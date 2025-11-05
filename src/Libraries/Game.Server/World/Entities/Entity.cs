using QuantumCore.API;
using QuantumCore.API.Core.Models;
using QuantumCore.API.Core.Utils;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Combat;
using QuantumCore.API.Game.World;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.Core.Utils;
using QuantumCore.Game.Constants;
using QuantumCore.Game.Extensions;
using QuantumCore.Game.Packets;
using QuantumCore.API.Systems.Affects;
using static QuantumCore.API.Game.Types.Combat.EImmunityFlags;
using static QuantumCore.Game.Constants.SchedulingConstants;

namespace QuantumCore.Game.World.Entities
{
    public abstract class Entity : IEntity
    {
        private readonly IAnimationManager _animationManager;
        public uint Vid { get; }
        public EEmpire Empire { get; private protected set; }
        public abstract EEntityType Type { get; }
        public uint EntityClass { get; protected set; }
        public EEntityState State { get; protected set; }
        public virtual IEntity? Target { get; set; }

        public int PositionX
        {
            get => _positionX;
            set
            {
                _positionChanged = _positionChanged || _positionX != value;
                _positionX = value;
            }
        }

        public int PositionY
        {
            get => _positionY;
            set
            {
                _positionChanged = _positionChanged || _positionY != value;
                _positionY = value;
            }
        }

        public float Rotation
        {
            get => _rotation;
            set => _rotation = value;
        }

        public bool PositionChanged
        {
            get => _positionChanged;
            set => _positionChanged = value;
        }

        public long Health { get; set; }
        public long Mana { get; set; }
        public abstract byte HealthPercentage { get; }
        public bool Dead { get; protected set; }

        public IMap? Map { get; set; }

        // QuadTree cache
        public int LastPositionX { get; set; }
        public int LastPositionY { get; set; }
        public IQuadTree? LastQuadTree { get; set; }

        // Movement related
        public long MovementStart { get; private set; }
        public int TargetPositionX { get; private set; }
        public int StartPositionX { get; private set; }
        public int TargetPositionY { get; private set; }
        public int StartPositionY { get; private set; }
        public uint MovementDuration { get; private set; }
        public byte MovementSpeed { get; set; }
        public byte AttackSpeed { get; set; }

        public IReadOnlyCollection<IEntity> NearbyEntities => _nearbyEntities;
        private readonly List<IEntity> _nearbyEntities = new();
        public List<IPlayerEntity> TargetedBy { get; } = new();
        public const int ViewDistance = 10000;

        private int _positionX;
        private int _positionY;
        private float _rotation;
        private bool _positionChanged;
        protected PlayerEntity? LastAttacker { get; private set; }

        // when set, entity is in knockout phase and will be dead shortly after
        private long? _knockedOutServerTime;

        public Entity(IAnimationManager animationManager, uint vid)
        {
            _animationManager = animationManager;
            Vid = vid;
        }

        protected abstract void OnNewNearbyEntity(IEntity entity);
        protected abstract void OnRemoveNearbyEntity(IEntity entity);
        public abstract void OnDespawn();
        public abstract void ShowEntity(IConnection connection);
        public abstract void HideEntity(IConnection connection);

        public virtual void Update(double elapsedTime)
        {
            if (!Dead && _knockedOutServerTime.HasValue && GameServer.Instance.ServerTime >= _knockedOutServerTime.Value + KnockoutToDeathDelaySeconds * 1000)
            {
                _knockedOutServerTime = null;
                Die();
            }

            if (State == EEntityState.Moving)
            {
                var elapsed = GameServer.Instance.ServerTime - MovementStart;
                var rate = MovementDuration == 0 ? 1 : elapsed / (float)MovementDuration;
                if (rate > 1) rate = 1;

                var x = (int)((TargetPositionX - StartPositionX) * rate + StartPositionX);
                var y = (int)((TargetPositionY - StartPositionY) * rate + StartPositionY);

                PositionX = x;
                PositionY = y;

                if (rate >= 1)
                {
                    State = EEntityState.Idle;
                }
            }
        }

        public virtual void Move(int x, int y)
        {
            if (_knockedOutServerTime.HasValue || (this is IAffectable a && a.Affects.GetActiveFlags().Has(EAffect.Stun)))
            {
                return;
            }
            if (PositionX == x && PositionY == y) return;

            PositionX = x;
            PositionY = y;
            PositionChanged = true;
        }

        public void Goto(Coordinates position) => Goto((int)position.X, (int)position.Y);

        public virtual void Goto(int x, int y)
        {
            if (_knockedOutServerTime.HasValue || (this is IAffectable a && a.Affects.GetActiveFlags().Has(EAffect.Stun)))
            {
                return;
            }
            if (PositionX == x && PositionY == y) return;
            if (TargetPositionX == x && TargetPositionY == y) return;

            var animation =
                _animationManager.GetAnimation(EntityClass, GetMovementAnimationType(), AnimationSubType.General);

            State = EEntityState.Moving;
            TargetPositionX = x;
            TargetPositionY = y;
            StartPositionX = PositionX;
            StartPositionY = PositionY;
            MovementStart = GameServer.Instance.ServerTime;

            var distance = MathUtils.Distance(StartPositionX, StartPositionY, TargetPositionX, TargetPositionY);
            double animationSpeed;
            if (animation is not null)
            {
                animationSpeed = -animation.AccumulationY / animation.MotionDuration;
            }
            else
            {
                // fallback duration when animation data is missing: approximate by movement speed
                // base speed: at MovementSpeed 100, travel 1000 units per second
                const double BaseUnitsPerSecondAt100 = 1000.0;
                animationSpeed = BaseUnitsPerSecondAt100;
            }

            var i = 100 - MovementSpeed;
            if (i > 0)
            {
                i = 100 + i;
            }
            else if (i < 0)
            {
                i = 10000 / (100 - i);
            }
            else
            {
                i = 100;
            }

            var duration = (int)((distance / animationSpeed) * 1000) * i / 100;
            MovementDuration = (uint)duration;
        }

        protected virtual AnimationType GetMovementAnimationType() => AnimationType.Run;

        public virtual void Wait(int x, int y)
        {
            // todo: Verify position possibility
            PositionX = x;
            PositionY = y;
        }

        public void Stop()
        {
            State = EEntityState.Idle;
            MovementDuration = 0;
        }

        public abstract EBattleType GetBattleType();
        public abstract int GetMinDamage();
        public abstract int GetMaxDamage();
        public abstract int GetBonusDamage();
        public abstract void AddPoint(EPoint point, int value);
        public abstract void SetPoint(EPoint point, uint value);
        public abstract uint GetPoint(EPoint point);

        public bool TryAttack(IEntity victim)
        {
            if (_knockedOutServerTime.HasValue || (this is IAffectable a && a.Affects.GetActiveFlags().Has(EAffect.Stun)))
            {
                return false;
            }
            if (this.PositionIsAttr(EMapAttribute.NonPvp))
            {
                return false;
            }

            if (victim.PositionIsAttr(EMapAttribute.NonPvp))
            {
                return false;
            }

            switch (GetBattleType())
            {
                case EBattleType.Melee:
                case EBattleType.Power:
                case EBattleType.Tanker:
                case EBattleType.SuperPower:
                case EBattleType.SuperTanker:
                    // melee sort attack
                    MeleeAttack(victim);
                    break;
                case EBattleType.Range:

                    RangeAttack(victim);
                    break;
                case EBattleType.Magic:
                    // todo magic attack
                    break;
            }

            // TODO: add validation and return true only if attacks were successful above
            return true;
        }

        private void MeleeAttack(IEntity victim)
        {
            // todo verify victim is in range
            var damage = ScalingFormulas.ComputeMeleeDamage(this, victim);
            // todo reduce damage by weapon type resist

            victim.Damage(this, EDamageType.Normal, damage);
        }

        private void RangeAttack(IEntity victim)
        {
            // todo verify victim is in range
            var damage = ScalingFormulas.ComputeMeleeDamage(this, victim);
            // todo reduce damage by weapon type resist

            foreach (var player in NearbyEntities.Where(x => x is IPlayerEntity).Cast<IPlayerEntity>())
            {
                player.Connection.Send(new ProjectilePacket
                {
                    TargetX = victim.PositionX, TargetY = victim.PositionY, Target = victim.Vid, Shooter = Vid
                });
            }

            victim.Damage(this, EDamageType.NormalRange, damage);
        }

        private int CalculateExperience(uint playerLevel)
        {
            var baseExp = GetPoint(EPoint.Experience);
            var entityLevel = GetPoint(EPoint.Level);

            var percentage = ExperienceConstants.GetExperiencePercentageByLevelDifference(playerLevel, entityLevel);

            return (int)(baseExp * percentage);
        }

        private void SendDebugDamage(IEntity other, string text)
        {
            if (this is PlayerEntity thisPlayer)
            {
                thisPlayer.SendChatInfo(text);
            }

            if (other is PlayerEntity otherPlayer)
            {
                otherPlayer.SendChatInfo(text);
            }
        }

        public virtual int Damage(IEntity attacker, EDamageType damageType, int damage)
        {

            if (this.PositionIsAttr(EMapAttribute.NonPvp))
            {
                SendDebugDamage(attacker,
                    $"{attacker}->{this} Ignoring damage inside NoPvP zone -> {damage}");
                return -1;
            }

            // DoT types do not crit/penetrate
            if (damageType is EDamageType.Poison or EDamageType.Fire)
            {
                // Poison does not kill
                if (damageType == EDamageType.Poison && Health - damage <= 0)
                {
                    damage = (int)Math.Max(0, Health - 1);
                }

                // deliver damage immediately without further calculations
                ApplyDamageAndBroadcast(attacker, damage, 
                    damageType == EDamageType.Poison ? EDamageFlags.Poison : EDamageFlags.Normal);
                return damage;
            }
            
            if (damageType is EDamageType.Normal or EDamageType.NormalRange)
            {
                TryApplyOnHitDebuffs(attacker);
            }
            else
            {
                // For other damage types (Melee, Range, Magic, etc.), proceed without on-hit affects
                // throw new NotImplementedException();
            }

            // todo block
            // todo handle berserk, fear, blessing skill
            // todo handle reflect melee

            SendDebugDamage(attacker, $"{attacker}->{this} Base Damage: {damage}");

            var damageFlags = EDamageFlags.Normal;

            var criticalPercentage = (int)attacker.GetPoint(EPoint.CriticalPercentage);
            if (criticalPercentage > 0)
            {
                var resist = (int)GetPoint(EPoint.ResistCritical);
                if (CoreRandom.PercentageCheck(criticalPercentage - resist))
                {
                    damageFlags |= EDamageFlags.Critical;
                    damage *= 2;
                    if (this is IAffectable affectable)
                    {
                        affectable.Affects.RemoveAllOfType(AffectType.FromSkill(ESkill.DarkProtection));
                    }
                    SendDebugDamage(attacker,
                        $"{attacker}->{this} Critical hit -> {damage} (percentage was {criticalPercentage - resist})");
                }
            }

            var penetratePercentage = (int)attacker.GetPoint(EPoint.PenetratePercentage);
            // todo add penetrate chance from passive
            if (penetratePercentage > 0)
            {
                var resist = (int)GetPoint(EPoint.ResistPenetrate);
                if (CoreRandom.PercentageCheck(penetratePercentage - resist))
                {
                    damageFlags |= EDamageFlags.Piercing;
                    damage += (int)GetPoint(EPoint.Defence);
                    if (this is IAffectable affectable)
                    {
                        affectable.Affects.RemoveAllOfType(AffectType.FromSkill(ESkill.DarkProtection));
                    }
                    SendDebugDamage(attacker,
                        $"{attacker}->{this} Penetrate hit -> {damage} (percentage was {penetratePercentage - resist})");
                }
            }

            // todo calculate hp steal, sp steal, hp recovery, sp recovery and mana burn
            // todo modify damage by active skill buffs/debuffs

            ApplyDamageAndBroadcast(attacker, damage, damageFlags);
            return damage;
        }

        private void ApplyDamageAndBroadcast(IEntity attacker, int damage, EDamageFlags damageFlags)
        {
            if (damageFlags.HasFlag(EDamageFlags.Critical))
            {
                this.BroadcastCharacterFx(ECharacterFx.Critical);
            }

            if (damageFlags.HasFlag(EDamageFlags.Piercing))
            {
                this.BroadcastCharacterFx(ECharacterFx.Penetrate);
            }
            
            var victimPlayer = this as PlayerEntity;
            var attackerPlayer = attacker as PlayerEntity;
            var damageInfo = new DamageInfo
            {
                Vid = Vid,
                Damage = damage,
                DamageFlags = (byte)damageFlags
            };

            if (victimPlayer != null)
            {
                victimPlayer.Connection.Send(damageInfo);
            }

            if (attackerPlayer != null)
            {
                attackerPlayer.Connection.Send(damageInfo);
                LastAttacker = attackerPlayer;
            }

            this.Health -= damage;

            if (victimPlayer != null)
            {
                victimPlayer.SendPoints();
            }

            foreach (var playerEntity in TargetedBy)
            {
                playerEntity.SendTarget();
            }

            if (Health <= 0)
            {
                if (!_knockedOutServerTime.HasValue)
                {
                    this.BroadcastNearby(new KnockoutCharacter { Vid = Vid });

                    _knockedOutServerTime = GameServer.Instance.ServerTime;
                }
            }
        }

        private void TryApplyOnHitDebuffs(IEntity attacker)
        {
            if (this is not IAffectable target)
            {
                return;
            }

            #region Poison
            
            var poisonChance = (int)attacker.GetPoint(EPoint.PoisonPercentage) - (int)GetPoint(EPoint.PoisonReduce);
            // if attacker level lower than target, chance to fail
            if (poisonChance > 0 && attacker.GetPoint(EPoint.Level) < GetPoint(EPoint.Level))
            {
                var atkLevelsUnder = GetPoint(EPoint.Level) - attacker.GetPoint(EPoint.Level);
                uint successPercentage = atkLevelsUnder switch
                {
                    < 3 => 100 - 10 * atkLevelsUnder,        // 0: 100%, 1: 90%, 2: 80%
                    < 7 =>  70 - 20 * (atkLevelsUnder - 3),  // 3: 70%, 4: 50%, 5: 30%, 6: 10%
                    < 9 =>   5,
                    _ =>     0
                };

                if (!CoreRandom.PercentageCheck(successPercentage))
                {
                    poisonChance = 0;
                }
            }

            if (!target.Affects.GetActiveFlags().Has(EAffect.Poison) &&
                CoreRandom.PercentageCheck(poisonChance))
            {
                target.Affects.Upsert(new EntityAffect
                {
                    AffectType = AffectType.From(EAffectType.Poison),
                    AffectFlag = EAffect.Poison,
                    RemainingDuration = TimeSpan.FromSeconds(DefaultPoisonDurationSeconds),
                    DoNotPersist = true,
                    SourceAttackerId = attacker.Vid
                });
            }
            
            #endregion

            #region Stun
            
            if (!this.TryImmunityCheck(StunImmunity) && 
                CoreRandom.PercentageCheck(attacker.GetPoint(EPoint.StunPercentage)))
            {
                Console.WriteLine($"Stunned: {attacker.GetPoint(EPoint.StunPercentage)}");
                var isPvm = attacker is PlayerEntity && this is MonsterEntity;
                target.Affects.Upsert(new EntityAffect
                {
                    AffectType = AffectType.From(EAffectType.Stun),
                    AffectFlag = EAffect.Stun,
                    RemainingDuration = TimeSpan.FromSeconds(isPvm ? PvmStunDurationSeconds : DefaultStunDurationSeconds),
                    DoNotPersist = true
                });
            }
            
            #endregion

            #region Slow
            
            if (!this.TryImmunityCheck(SlowImmunity) && 
                CoreRandom.PercentageCheck(attacker.GetPoint(EPoint.SlowPercentage)))
            {
                target.Affects.Upsert(new EntityAffect
                {
                    AffectType = AffectType.From(EAffectType.Slow),
                    AffectFlag = EAffect.Slow,
                    ModifiedPointId = EPoint.MoveSpeed,
                    ModifiedPointDelta = DefaultMovementDebuffValue,
                    RemainingDuration = TimeSpan.FromSeconds(DefaultSlowDurationSeconds),
                    DoNotPersist = true
                });
            }

            #endregion
        }

        public virtual void Die()
        {
            Dead = true;
        }

        public void AddNearbyEntity(IEntity entity)
        {
            _nearbyEntities.Add(entity);
            OnNewNearbyEntity(entity);
        }

        public void RemoveNearbyEntity(IEntity entity)
        {
            if (_nearbyEntities.Remove(entity))
            {
                OnRemoveNearbyEntity(entity);
            }
        }

        public void ForEachNearbyEntity(Action<IEntity> action)
        {
            foreach (var entity in _nearbyEntities)
            {
                action(entity);
            }
        }
    }
}
