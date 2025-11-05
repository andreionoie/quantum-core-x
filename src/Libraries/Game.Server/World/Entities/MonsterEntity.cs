using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuantumCore.API;
using QuantumCore.API.Core.Models;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Combat;
using QuantumCore.API.Game.Types.Monsters;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Game.World;
using QuantumCore.API.Game.World.AI;
using QuantumCore.API.Systems.Affects;
using QuantumCore.API.Systems.Stats;
using QuantumCore.Core.Utils;
using QuantumCore.Game.Packets;
using QuantumCore.Game.Services;
using QuantumCore.Game.World.AI;
using QuantumCore.Game.Systems.Tickers;
using QuantumCore.API.Systems.Tickers;
using QuantumCore.Game.Extensions;
using static QuantumCore.API.Game.Types.Combat.EImmunityFlags;

namespace QuantumCore.Game.World.Entities
{
    public class MonsterEntity : Entity, IMonsterEntity
    {
        private readonly StatEngine _stats;
        private readonly IDropProvider _dropProvider;
        private readonly ILogger _logger;
        public override EEntityType Type => (EEntityType)Proto.Type;
        public bool IsStone => Proto.Type == (byte)EEntityType.MetinStone;
        public EMonsterLevel Rank => (EMonsterLevel)Proto.Rank;

        public override IEntity? Target
        {
            get
            {
                return (_behaviour as SimpleBehaviour)?.Target;
            }
            set
            {
                if (_behaviour is SimpleBehaviour sb)
                {
                    sb.Target = value;
                }
            }
        }

        public IBehaviour? Behaviour
        {
            get { return _behaviour; }
            set
            {
                _behaviour = value;
                _behaviourInitialized = false;
            }
        }

        public override byte HealthPercentage
        {
            get { return (byte)(Math.Min(Math.Max(Health / (double)Proto.Hp, 0), 1) * 100); }
        }

        public IAffectsController Affects { get; }
        
        public MonsterData Proto { get; private set; }

        public MonsterGroup? Group { get; set; }

        private IBehaviour? _behaviour;
        private bool _behaviourInitialized;
        private double _deadTime = 5000;
        private readonly IMap _map;
        private readonly IItemManager _itemManager;
        private IServiceProvider _serviceProvider;
        
        private readonly GatedTickerEngine<IMonsterEntity> _affectsTicker;
        private readonly GatedTickerEngine<IMonsterEntity> _poisonTicker;
        private readonly GatedTickerEngine<IMonsterEntity> _fireTicker;
        private readonly MonsterHpPassiveRestoreTicker _hpPassiveRestoreTicker;

        public MonsterEntity(IMonsterManager monsterManager, IDropProvider dropProvider,
            IAnimationManager animationManager,
            IServiceProvider serviceProvider,
            IMap map, ILogger logger, IItemManager itemManager, uint id, int x, int y, float rotation = 0)
            : base(animationManager, map.World.GenerateVid())
        {
            var proto = monsterManager.GetMonster(id);

            if (proto is null)
            {
                // todo handle better
                throw new InvalidOperationException($"Could not find mob proto for ID {id}. Cannot create mob entity");
            }

            _map = map;
            _dropProvider = dropProvider;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _itemManager = itemManager;
            Proto = proto;
            PositionX = x;
            PositionY = y;
            Rotation = rotation;

            MovementSpeed = (byte)Proto.MoveSpeed;

            Health = Proto.Hp;
            EntityClass = id;

            _stats = new StatEngine(monsterManager.BasePointsForMonster(id));
            Affects = new AffectsController();
            Affects.AffectAdded += OnAffectAdded;
            Affects.AffectRemoved += OnAffectRemoved;

            // initialize computed speeds to match base stats
            SyncSpeedsFromComputedStats();

            _affectsTicker = new AffectsTicker<IMonsterEntity>(this);
            _poisonTicker = new PoisonDamageOverTimeTicker<IMonsterEntity>(this);
            _fireTicker = new FireDamageOverTimeTicker<IMonsterEntity>(this);
            _hpPassiveRestoreTicker = new MonsterHpPassiveRestoreTicker(this);

            if (Proto.Type == (byte)EEntityType.Monster)
            {
                // it's a monster
                _behaviour = new SimpleBehaviour(monsterManager);
            }
            else if (Proto.Type == (byte)EEntityType.Npc)
            {
                // npc
            }
            else if (Proto.Type == (byte)EEntityType.MetinStone)
            {
                _behaviour = ActivatorUtilities.CreateInstance<StoneBehaviour>(_serviceProvider);
            }
        }

        public override void Update(double elapsedTime)
        {
            if (Map is null) return;
            if (Dead)
            {
                _deadTime -= elapsedTime;
                if (_deadTime <= 0)
                {
                    Map.DespawnEntity(this);
                }
            }

            if (!_behaviourInitialized)
            {
                _behaviour?.Init(this);
                _behaviourInitialized = true;
            }

            if (!Dead)
            {
                _behaviour?.Update(elapsedTime);
            }

            base.Update(elapsedTime);
            
            var hpChanged = false;
            var elapsed = TimeSpan.FromMilliseconds(elapsedTime);
            _affectsTicker.Step(elapsed);
            hpChanged |= _poisonTicker.Step(elapsed);
            hpChanged |= _fireTicker.Step(elapsed);
            hpChanged |= _hpPassiveRestoreTicker.Step(elapsed);
            if (hpChanged)
            {
                foreach (var player in TargetedBy)
                {
                    player.SendTarget();
                }
            }
        }

        public override void Goto(int x, int y)
        {
            Rotation = (float)MathUtils.Rotation(x - PositionX, y - PositionY);

            base.Goto(x, y);
            // Send movement to nearby players
            var movement = new CharacterMoveOut
            {
                Vid = Vid,
                Rotation = (byte)(Rotation / 5),
                Argument = (byte)CharacterMove.CharacterMovementType.Wait,
                PositionX = TargetPositionX,
                PositionY = TargetPositionY,
                Time = (uint)GameServer.Instance.ServerTime,
                Duration = MovementDuration
            };

            foreach (var entity in NearbyEntities)
            {
                if (entity is PlayerEntity player)
                {
                    player.Connection.Send(movement);
                }
            }
        }

        public override EBattleType GetBattleType()
        {
            return Proto.BattleType;
        }

        public override int GetMinDamage()
        {
            return (int)Proto.DamageRange[0];
        }

        public override int GetMaxDamage()
        {
            return (int)Proto.DamageRange[1];
        }

        public override int GetBonusDamage()
        {
            return 0; // monster don't have bonus damage as players have from their weapon
        }

        public override int Damage(IEntity attacker, EDamageType damageType, int damage)
        {
            damage = base.Damage(attacker, damageType, damage);

            if (damage >= 0)
            {
                Behaviour?.TookDamage(attacker, (uint)damage);
                Group?.TriggerAll(attacker, this);
            }

            return damage;
        }

        public void Trigger(IEntity attacker)
        {
            Behaviour?.TookDamage(attacker, 0);
        }

        public override void AddPoint(EPoint point, int value)
        {
        }

        public override void SetPoint(EPoint point, uint value)
        {
        }

        public override uint GetPoint(EPoint point)
        {
            switch (point)
            {
                #region Runtime State
                
                case EPoint.Hp:
                    return (uint)Health;
                
                #endregion
                
                #region Bonuses
                
                case EPoint.AttackBonus or EPoint.AttackGradeBonus:
                case EPoint.MagicAttackBonusPer or EPoint.MeleeMagicAttackBonusPer or EPoint.MagicAttackGradeBonus:
                case EPoint.AttackBonusHuman or EPoint.AttackBonusAnimal or EPoint.AttackBonusOrc
                    or EPoint.AttackBonusEsoterics or EPoint.AttackBonusUndead or EPoint.AttackBonusDevil
                    or EPoint.AttackBonusInsect or EPoint.AttackBonusFire or EPoint.AttackBonusIce
                    or EPoint.AttackBonusDesert or EPoint.AttackBonusMonster or EPoint.AttackBonusWarrior
                    or EPoint.AttackBonusAssassin or EPoint.AttackBonusSura or EPoint.AttackBonusShaman
                    or EPoint.AttackBonusTree:
                case EPoint.DefenceBonus:
                    return 0;
                
                #endregion
                
                case EPoint.Level:
                    return Proto.Level;
                case EPoint.MaxHp:
                    return Proto.Hp;
                case EPoint.St:
                    return Proto.St;
                case EPoint.Ht:
                    return Proto.Ht;
                case EPoint.Dx:
                    return Proto.Dx;
                case EPoint.Iq:
                    return Proto.Iq;
                case EPoint.Experience:
                    return Proto.Experience;
                
                #region Immunities
                
                case EPoint.ImmuneStun:
                    return ((EImmunityFlags)Proto.ImmuneFlag).HasFlag(StunImmunity) ? 1u : 0;
                case EPoint.ImmuneSlow:
                    return ((EImmunityFlags)Proto.ImmuneFlag).HasFlag(SlowImmunity) ? 1u : 0;
                case EPoint.ImmuneFall:
                    return ((EImmunityFlags)Proto.ImmuneFlag).HasFlag(FallImmunity) ? 1u : 0;
                
                #endregion
                
                #region Enchantments
                
                case EPoint.PoisonPercentage:
                    return Proto.Enchantments.ElementAtOrDefault((int)EMonsterEnchantment.Poison);
                case EPoint.StunPercentage:
                    return Proto.Enchantments.ElementAtOrDefault((int)EMonsterEnchantment.Stun);
                case EPoint.SlowPercentage:
                    return Proto.Enchantments.ElementAtOrDefault((int)EMonsterEnchantment.Slow);
                case EPoint.CriticalPercentage:
                    return Proto.Enchantments.ElementAtOrDefault((int)EMonsterEnchantment.Critical);
                case EPoint.PenetratePercentage:
                    return Proto.Enchantments.ElementAtOrDefault((int)EMonsterEnchantment.Penetrate);
                case EPoint.CursePercentage:
                    return Proto.Enchantments.ElementAtOrDefault((int)EMonsterEnchantment.Curse);
                
                #endregion

                #region Resists
                
                case EPoint.ResistNormalDamage or EPoint.ResistCritical or EPoint.ResistPenetrate:
                case EPoint.ResistIce or EPoint.ResistEarth or EPoint.ResistDark:
                case EPoint.ResistWarrior or EPoint.ResistAssassin or EPoint.ResistSura or EPoint.ResistShaman:
                    return 0;
                
                case EPoint.ResistSword:
                    return Proto.Resists.ElementAtOrDefault((int)EMonsterResistance.Sword);
                case EPoint.ResistTwoHanded:
                    return Proto.Resists.ElementAtOrDefault((int)EMonsterResistance.TwoHanded);
                case EPoint.ResistDagger:
                    return Proto.Resists.ElementAtOrDefault((int)EMonsterResistance.Dagger);
                case EPoint.ResistBell:
                    return Proto.Resists.ElementAtOrDefault((int)EMonsterResistance.Bell);
                case EPoint.ResistFan:
                    return Proto.Resists.ElementAtOrDefault((int)EMonsterResistance.Fan);
                case EPoint.ResistBow:
                    return Proto.Resists.ElementAtOrDefault((int)EMonsterResistance.Bow);
                case EPoint.ResistFire:
                    return Proto.Resists.ElementAtOrDefault((int)EMonsterResistance.Fire);
                case EPoint.ResistElectric:
                    return Proto.Resists.ElementAtOrDefault((int)EMonsterResistance.Electric);
                case EPoint.ResistMagic:
                    return Proto.Resists.ElementAtOrDefault((int)EMonsterResistance.Magic);
                case EPoint.ResistWind:
                    return Proto.Resists.ElementAtOrDefault((int)EMonsterResistance.Wind);
                case EPoint.PoisonReduce:
                    return Proto.Resists.ElementAtOrDefault((int)EMonsterResistance.Poison);

                #endregion
            }

            return (uint)_stats[point];
        }

        public override void Die()
        {
            if (Dead)
            {
                return;
            }

            CalculateDrops();

            base.Die();

            var dead = new CharacterDead {Vid = Vid};
            foreach (var entity in NearbyEntities)
            {
                if (entity is PlayerEntity player)
                {
                    player.Connection.Send(dead);
                }
            }

            // clear target UI for all players targeting this entity
            var clearTargetPacket = new SetTarget { TargetVid = 0 };
            foreach (var targetingPlayer in TargetedBy)
            {
                targetingPlayer.Connection.Send(clearTargetPacket);
            }
            TargetedBy.Clear();
        }

        private void CalculateDrops()
        {
            // no drops if no killer
            if (LastAttacker is null) return;

            var drops = new List<ItemInstance>();

            var (delta, range) = _dropProvider.CalculateDropPercentages(LastAttacker, this);

            // Common drops (common_drop_item.txt)
            drops.AddRange(_dropProvider.CalculateCommonDropItems(LastAttacker, this, delta, range));

            // Drop Item Group (mob_drop_item.txt)
            drops.AddRange(_dropProvider.CalculateDropItemGroupItems(this, delta, range));

            // Mob Drop Item Group (mob_drop_item.txt)
            drops.AddRange(_dropProvider.CalculateMobDropItemGroupItems(LastAttacker, this, delta, range));

            // Level drops (mob_drop_item.txt)
            drops.AddRange(_dropProvider.CalculateLevelDropItems(LastAttacker, this, delta, range));

            // Etc item drops (etc_drop_item.txt)
            drops.AddRange(_dropProvider.CalculateEtcDropItems(this, delta, range));

            if (IsStone)
            {
                // Spirit stone drops
                drops.AddRange(_dropProvider.CalculateMetinDropItems(this, delta, range));
            }

            // todo:
            // - horse riding skill drops
            // - quest item drops
            // - event item drops

            // Finally, drop the items
            foreach (var drop in drops)
            {
                // todo: if drop is yang, adjust the amount in function below instead of '1'
                _map.AddGroundItem(drop, PositionX, PositionY, 1, LastAttacker.Name);
            }
        }

        protected override void OnNewNearbyEntity(IEntity entity)
        {
            _behaviour?.OnNewNearbyEntity(entity);
        }

        protected override void OnRemoveNearbyEntity(IEntity entity)
        {
        }

        public override void OnDespawn()
        {
            if (Group != null)
            {
                Group.Monsters.Remove(this);
                if (Group.Monsters.Count == 0)
                {
                    (Map as Map)?.EnqueueGroupRespawn(Group);
                }
            }
        }

        public override void ShowEntity(IConnection connection)
        {
            if (Dead)
            {
                return; // no need to send dead entities to new players
            }

            connection.Send(new SpawnCharacter
            {
                Vid = Vid,
                CharacterType = Proto.Type,
                Angle = Rotation,
                PositionX = PositionX,
                PositionY = PositionY,
                Class = (ushort)Proto.Id,
                MoveSpeed = (byte)Proto.MoveSpeed,
                AttackSpeed = (byte)Proto.AttackSpeed,
                Affects = (ulong)Affects.GetActiveFlags()
                // TODO: animation on spawn with State = ESpawnStateFlags.WithFallingAnimation
                // TODO: particle effect on spawn with Affects = EAffectFlags.SpawnWithAppearFx
            });

            if (Proto.Type == (byte)EEntityType.Npc)
            {
                // NPCs need additional information too to show up for some reason
                connection.Send(new CharacterInfo
                {
                    Vid = Vid, Empire = Proto.Empire, Level = 0, Name = Proto.TranslatedName
                });
            }
        }

        public override void HideEntity(IConnection connection)
        {
            connection.Send(new RemoveCharacter {Vid = Vid});
        }


        public override string ToString()
        {
            return $"{Proto.TranslatedName?.Trim((char)0x00)} ({Proto.Id})";
        }


        private void OnAffectAdded(EntityAffect affect)
        {
            if (affect.ModifiedPointId != EPoint.None && affect.ModifiedPointDelta != 0)
            {
                _stats[affect.ModifiedPointId] += (affect.ModifiedPointDelta, affect);
                SyncSpeedsFromComputedStats();
            }
            BroadcastAffectFlags();
        }

        private void OnAffectRemoved(EntityAffect affect)
        {
            if (affect.ModifiedPointId != EPoint.None)
            {
                if (_stats[affect.ModifiedPointId].RemoveModifier(affect))
                {
                    SyncSpeedsFromComputedStats();
                }
            }
            BroadcastAffectFlags();
        }

        private void SyncSpeedsFromComputedStats()
        {
            AttackSpeed = (byte)Math.Clamp(_stats[EPoint.AttackSpeed], 0, byte.MaxValue);
            MovementSpeed = (byte)Math.Clamp(_stats[EPoint.MoveSpeed], 0, byte.MaxValue);
        }

        private void BroadcastAffectFlags()
        {
            var updatePacket = new CharacterUpdate
            {
                Vid = Vid,
                MoveSpeed = MovementSpeed,
                AttackSpeed = AttackSpeed,
                Affects = (ulong)Affects.GetActiveFlags(),
                State = (byte)State
            };

            this.BroadcastNearby(updatePacket);
        }
    }
}
