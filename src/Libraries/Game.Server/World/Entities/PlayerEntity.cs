using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuantumCore.API;
using QuantumCore.API.Core.Models;
using QuantumCore.API.Extensions;
using QuantumCore.API.Game.Guild;
using QuantumCore.API.Game.Skills;
using QuantumCore.API.Game.Types;
using QuantumCore.API.Game.Types.Combat;
using QuantumCore.API.Game.Types.Items;
using QuantumCore.API.Game.Types.Skills;
using QuantumCore.API.Game.World;
using QuantumCore.API.Systems.Affects;
using QuantumCore.API.Systems.Mobility;
using QuantumCore.API.Systems.Stats;
using QuantumCore.API.Systems.Tickers;
using QuantumCore.Caching;
using QuantumCore.Core.Event;
using QuantumCore.Extensions;
using QuantumCore.Game.Constants;
using QuantumCore.Game.Extensions;
using QuantumCore.Game.Packets;
using QuantumCore.Game.Packets.Guild;
using QuantumCore.Game.Persistence;
using QuantumCore.Game.PlayerUtils;
using QuantumCore.Game.Skills;
using QuantumCore.Game.Systems.Mobility;
using QuantumCore.Game.Systems.Tickers;

namespace QuantumCore.Game.World.Entities
{
    public class PlayerEntity : Entity, IPlayerEntity, IDisposable
    {
        public override EEntityType Type => EEntityType.Player;

        public string Name => Player.Name;
        public IGameConnection Connection { get; }
        public PlayerData Player { get; private set; }
        public GuildData? Guild { get; private set; }
        public IInventory Inventory { get; private set; }
        public IList<Guid> Groups { get; private set; }
        public IShop? Shop { get; set; }
        public IQuickSlotBar QuickSlotBar { get; }
        public IMobilityController Mobility { get; private set; }
        public IPlayerSkills Skills { get; private set; }
        public IAffectsController Affects { get; }
        public IQuest? CurrentQuest { get; set; }
        public Dictionary<string, IQuest> Quests { get; } = [];

        public string AllStatModifiers => _stats.ToString() ?? string.Empty;

        public override byte HealthPercentage
        {
            get
            {
                return 100; // todo
            }
        }

        public long MsSinceLastAttacked() => _lastAttackAt.HasValue ? 
            GameServer.Instance.ServerTime - _lastAttackAt.Value : int.MaxValue;

        public EAntiFlags AntiFlagClass
        {
            get
            {
                switch (Player.PlayerClass.GetClass())
                {
                    case EPlayerClass.Warrior:
                        return EAntiFlags.Warrior;
                    case EPlayerClass.Ninja:
                        return EAntiFlags.Assassin;
                    case EPlayerClass.Sura:
                        return EAntiFlags.Sura;
                    case EPlayerClass.Shaman:
                        return EAntiFlags.Shaman;
                    default:
                        return 0;
                }
            }
        }

        public EAntiFlags AntiFlagGender
        {
            get
            {
                switch (Player.PlayerClass.GetGender())
                {
                    case EPlayerGender.Male:
                        return EAntiFlags.Male;
                    case EPlayerGender.Female:
                        return EAntiFlags.Female;
                    default:
                        return 0;
                }
            }
        }

        private int _persistTime;

        private readonly GatedTickerEngine<IPlayerEntity> _hpPassiveRestoreTicker;
        private readonly GatedTickerEngine<IPlayerEntity> _spPassiveRestoreTicker;
        private readonly GatedTickerEngine<IPlayerEntity> _staminaFullRestoreTicker;
        private readonly GatedTickerEngine<IPlayerEntity> _hpRecoveryTicker;
        private readonly GatedTickerEngine<IPlayerEntity> _spRecoveryTicker;
        private readonly GatedTickerEngine<IPlayerEntity> _staminaConsumptionTicker;

        private readonly GatedTickerEngine<IPlayerEntity> _affectsTicker;
        private readonly SuraBmFlameSpiritTicker _flameSpiritHitTicker;
        private readonly GatedTickerEngine<IPlayerEntity> _poisonTicker;
        private readonly GatedTickerEngine<IPlayerEntity> _fireTicker;

        private readonly IItemManager _itemManager;
        private readonly IJobManager _jobManager;
        private readonly IExperienceManager _experienceManager;
        private readonly IQuestManager _questManager;
        private readonly ICacheManager _cacheManager;
        private readonly IWorld _world;
        private readonly ILogger<PlayerEntity> _logger;
        private readonly IServiceScope _scope;
        private readonly IItemRepository _itemRepository;
        private readonly IStatEngine _stats;

        private long? _lastAttackAt;
        private long? _diedAtMs;
        
        // Runtime-only recovery buckets (not persisted)
        private uint _hpRecoveryBucket;
        private uint _spRecoveryBucket;
        
        private long? _countdownEventId;        // the event for logout or phase_select

        public PlayerEntity(PlayerData player, IGameConnection connection, IItemManager itemManager,
            IJobManager jobManager,
            IExperienceManager experienceManager, IAnimationManager animationManager,
            IQuestManager questManager, ICacheManager cacheManager, IWorld world, ILogger<PlayerEntity> logger,
            IServiceProvider serviceProvider)
            : base(animationManager, world.GenerateVid())
        {
            Connection = connection;
            _itemManager = itemManager;
            _jobManager = jobManager;
            _experienceManager = experienceManager;
            _questManager = questManager;
            _cacheManager = cacheManager;
            _world = world;
            _logger = logger;
            _scope = serviceProvider.CreateScope();
            _itemRepository = _scope.ServiceProvider.GetRequiredService<IItemRepository>();
            Inventory = new Inventory(itemManager, _cacheManager, _logger, _itemRepository, player.Id,
                (byte)WindowType.Inventory, InventoryConstants.DEFAULT_INVENTORY_WIDTH,
                InventoryConstants.DEFAULT_INVENTORY_HEIGHT, InventoryConstants.DEFAULT_INVENTORY_PAGES);
            Inventory.OnSlotChanged += Inventory_OnSlotChanged;
            Player = player;
            Empire = player.Empire;
            PositionX = player.PositionX;
            PositionY = player.PositionY;
            QuickSlotBar = ActivatorUtilities.CreateInstance<QuickSlotBar>(_scope.ServiceProvider, this);
            Skills = ActivatorUtilities.CreateInstance<PlayerSkills>(_scope.ServiceProvider, this);

            MovementSpeed = PlayerConstants.DEFAULT_MOVEMENT_SPEED;
            AttackSpeed = PlayerConstants.DEFAULT_ATTACK_SPEED;
            EntityClass = (uint)player.PlayerClass;

            Groups = new List<Guid>();

            // init the computed stat engine (allows stacking various modifiers and easily removing them)
            _stats = new StatEngine(ScalingFormulas.BasePointsForPlayer(this, jobManager));
            ScalingFormulas.RegisterDependenciesForPlayer(_stats, player.PlayerClass.GetClass());
            // clamp HP/SP whenever MaxHp/MaxSp decreases to lower than the current HP/SP
            _stats[EPoint.MaxHp].OnChanged += _ => Health = Math.Clamp(Health, 0, (int)GetPoint(EPoint.MaxHp));
            _stats[EPoint.MaxSp].OnChanged += _ => Mana = Math.Clamp(Mana, 0, (int)GetPoint(EPoint.MaxSp));

            // init the affects engine
            Affects = new AffectsController();
            Affects.AffectAdded += OnAffectAdded;      // send affect add/remove packets and recompute stats
            Affects.AffectRemoved += OnAffectRemoved;
            
            // init the hp/sp/stamina/affects periodic tickers
            _hpPassiveRestoreTicker = new PlayerHpPassiveRestoreTicker(this);
            _hpRecoveryTicker = new PlayerHpRecoveryTicker(this);
            
            _spPassiveRestoreTicker = new PlayerSpPassiveRestoreTicker(this);
            _spRecoveryTicker = new PlayerSpRecoveryTicker(this);
            
            _staminaFullRestoreTicker = new PlayerStaminaFullRestoreTicker(this);
            _staminaConsumptionTicker = new PlayerStaminaConsumptionTicker(this);
            Mobility = new PlayerMobilityController();
            Mobility.ActiveModeChanged += OnMobilityModeChanged; // send walking animation packet on zero stamina
            
            _affectsTicker = new AffectsTicker<IPlayerEntity>(this, TryConsumeSpCost);
            _flameSpiritHitTicker = new SuraBmFlameSpiritTicker(this);
            _poisonTicker = new PoisonDamageOverTimeTicker<IPlayerEntity>(this);
            _fireTicker = new FireDamageOverTimeTicker<IPlayerEntity>(this);
        }

        private bool TryConsumeSpCost(uint spCost)
        {
            if (spCost > GetPoint(EPoint.Sp))
            {
                return false;
            }

            AddPoint(EPoint.Sp, -(int)spCost);
            return true;
        }

        public async Task Load()
        {
            Health = Math.Clamp(Player.Health, 0, GetPoint(EPoint.MaxHp));
            Mana = Math.Clamp(Player.Mana, 0, GetPoint(EPoint.MaxSp));
            // todo: try parallelize DB loading?
            await Inventory.Load();
            SyncEquipmentModifiers();
            await QuickSlotBar.Load();
            await LoadPermGroups();
            await Skills.LoadAsync();
            await LoadAffectsAsync();
            var guildManager = _scope.ServiceProvider.GetRequiredService<IGuildManager>();
            Guild = await guildManager.GetGuildForPlayerAsync(Player.Id);
            Player.GuildId = Guild?.Id;
            _questManager.InitializePlayer(this);
        }

        public async Task ReloadPermissions()
        {
            Groups.Clear();
            await LoadPermGroups();
        }

        private async Task LoadPermGroups()
        {
            var commandPermissionRepository = _scope.ServiceProvider.GetRequiredService<ICommandPermissionRepository>();
            var playerId = Player.Id;

            var groups = await commandPermissionRepository.GetGroupsForPlayer(playerId);

            foreach (var group in groups)
            {
                Groups.Add(group);
            }
        }

        private async Task LoadAffectsAsync()
        {
            var affectRepository = _scope.ServiceProvider.GetRequiredService<IPlayerAffectsRepository>();
            var affects = await affectRepository.GetPlayerAffectsAsync(Player.Id);

            foreach (var affect in affects)
            {
                Affects.Upsert(affect);
            }
        }

        private async Task PersistAffectsAsync()
        {
            var affectRepository = _scope.ServiceProvider.GetRequiredService<IDbPlayerAffectsRepository>();
            await affectRepository.SavePlayerAffectsAsync(Player.Id, Affects.Active);
        }

        public T? GetQuestInstance<T>() where T : class, IQuest
        {
            var id = typeof(T).FullName;
            if (id == null)
            {
                return default;
            }

            return (T)Quests[id];
        }

        private void Warp(Coordinates position) => Warp((int)position.X, (int)position.Y);

        private void Warp(int x, int y)
        {
            _world.DespawnEntity(this);

            PositionX = x;
            PositionY = y;

            var host = _world.GetMapHost(PositionX, PositionY);

            _logger.LogInformation("Warp!");
            var packet = new Warp
            {
                PositionX = PositionX,
                PositionY = PositionY,
                ServerAddress = BitConverter.ToInt32(host.Ip.GetAddressBytes()),
                ServerPort = host.Port
            };
            Connection.Send(packet);
        }

        public void Move(Coordinates position) => Move((int)position.X, (int)position.Y);

        public override void Move(int x, int y)
        {
            if (Map is null) return;
            if (PositionX == x && PositionY == y) return;

            if (!Map.IsPositionInside(x, y))
            {
                Warp(x, y);
                return;
            }

            if (Map is Map localMap &&
                localMap.IsAttr(new Coordinates((uint)x, (uint)y), EMapAttribute.Block | EMapAttribute.Object))
            {
                _logger.LogDebug("Not allowed to move character {Name} to map position ({X}, {Y}) with attributes Block or Object", Name, x, y);
                return;
            }

            PositionX = x;
            PositionY = y;

            // Reset movement info
            Stop();
        }

        protected override AnimationType GetMovementAnimationType()
            => Mobility.IsCurrentlyWalking ? AnimationType.Walk : AnimationType.Run;

        public override void Die()
        {
            if (Dead)
            {
                return;
            }

            base.Die();

            Affects.Clear(preserveNoClearOnDeath: true);

            var dead = new CharacterDead {Vid = Vid};
            foreach (var entity in NearbyEntities)
            {
                if (entity is PlayerEntity player)
                {
                    player.Connection.Send(dead);
                }
            }

            Connection.Send(dead);
            _diedAtMs = GameServer.Instance.ServerTime;
        }

        private void SendGuildInfo()
        {
            if (Guild is not null)
            {
                var onlineMemberIds = _world.GetGuildMembers(Guild.Id).Select(x => x.Player.Id).ToArray();
                Connection.SendGuildMembers(Guild.Members, onlineMemberIds);
                Connection.SendGuildRanks(Guild.Ranks);
                Connection.SendGuildInfo(Guild);
                Connection.Send(new GuildName {Id = Guild.Id, Name = Guild.Name});
            }
        }

        public async Task RefreshGuildAsync()
        {
            var guildManager = _scope.ServiceProvider.GetRequiredService<IGuildManager>();
            Guild = await guildManager.GetGuildForPlayerAsync(Player.Id);
            Player.GuildId = Guild?.Id;
            SendGuildInfo();
            SendCharacterUpdate();
        }

        public void Respawn(bool town)
        {
            if (!Dead)
            {
                return;
            }

            Shop?.Close(this);

            Dead = false;

            if (town)
            {
                var townCoordinates = Map!.TownCoordinates;
                if (townCoordinates is not null)
                {
                    Move(Player.Empire switch
                    {
                        EEmpire.Chunjo => townCoordinates.Chunjo,
                        EEmpire.Jinno => townCoordinates.Jinno,
                        EEmpire.Shinsoo => townCoordinates.Shinsoo,
                        _ => throw new ArgumentOutOfRangeException(nameof(Player.Empire),
                            $"Can't get empire coordinates for empire {Player.Empire}")
                    });
                }
            }

            Affects.Upsert(EntityAffect.InvisibleRespawn5Sec);
            _diedAtMs = null;

            SendChatCommand("CloseRestartWindow");
            Connection.SetPhase(EPhases.Game);

            var remove = new RemoveCharacter {Vid = Vid};

            Connection.Send(remove);
            ShowEntity(Connection);

            foreach (var entity in NearbyEntities)
            {
                if (entity is PlayerEntity pe)
                {
                    ShowEntity(pe.Connection);
                }

                entity.ShowEntity(Connection);
            }

            Health = PlayerConstants.RESPAWN_HEALTH;
            Mana = PlayerConstants.RESPAWN_MANA;
            SendPoints();
        }

        public void RecalculateStatusPoints()
        {
            var shouldHavePoints = (uint)((Player.Level - 1) * 3);
            var steps = (byte)Math.Floor(
                GetPoint(EPoint.Experience) / (double)GetPoint(EPoint.NeededExperience) * 4);
            shouldHavePoints += steps;

            if (shouldHavePoints <= Player.GivenStatusPoints)
            {
                // Remove available points if possible
                var tooMuch = Player.GivenStatusPoints - shouldHavePoints;
                if (Player.AvailableStatusPoints < tooMuch)
                {
                    tooMuch = Player.AvailableStatusPoints;
                }

                Player.AvailableStatusPoints -= tooMuch;
                Player.GivenStatusPoints -= tooMuch;

                return;
            }

            Player.AvailableStatusPoints += shouldHavePoints - Player.GivenStatusPoints;
            Player.GivenStatusPoints = shouldHavePoints;
        }

        private bool CheckLevelUp()
        {
            var exp = GetPoint(EPoint.Experience);
            var needed = GetPoint(EPoint.NeededExperience);

            if (needed > 0 && exp >= needed)
            {
                SetPoint(EPoint.Experience, exp - needed);
                LevelUp();

                if (!CheckLevelUp())
                {
                    SendPoints();
                }

                return true;
            }

            RecalculateStatusPoints();
            return false;
        }

        private void LevelUp(int level = 1)
        {
            // if experience table is not loaded (MaxLevel == 0), treat as uncapped for debug/admin commands
            var maxLevel = _experienceManager.MaxLevel == 0 ? byte.MaxValue : _experienceManager.MaxLevel;
            if (level == 0 || Player.Level + level > maxLevel)
            {
                return;
            }

            AddPoint(EPoint.Skill, level);
            AddPoint(EPoint.SubSkill, level < 10 ? 0 : level - Math.Max((int)Player.Level, 9));

            Player.Level = (byte)(Player.Level + level);
            _stats.NotifyBaseChanged(EPoint.Level);

            // todo: animation (I think this actually is a quest sent by the server on character login and not an actual packet at this stage)

            foreach (var entity in NearbyEntities)
            {
                if (entity is not IPlayerEntity other) continue;
                SendCharacterAdditional(other.Connection);
            }

            RecalculateStatusPoints();
            SendPoints();
        }

        public uint CalculateAttackDamage(uint baseDamage)
        {
            var attackStatus = _jobManager.Get(Player.PlayerClass)?.AttackStatus;

            if (attackStatus is null) return 0;

            var levelBonus = GetPoint(EPoint.Level) * 2;
            var statusBonus = (
                4 * GetPoint(EPoint.St) +
                2 * GetPoint(attackStatus.Value)
            ) / 3;
            var weaponDamage = baseDamage * 2;

            return levelBonus + (statusBonus + weaponDamage) * GetHitRate() / 100;
        }

        public uint GetHitRate() => GetPoint(EPoint.HitRate);

        public override void Update(double elapsedTime)
        {
            if (Map == null) return; // We don't have a map yet so we aren't spawned

            // auto respawn if the dead timeout elapsed
            if (Dead && _diedAtMs.HasValue)
            {
                var now = GameServer.Instance.ServerTime;
                var deadline = _diedAtMs.Value + SchedulingConstants.PlayerAutoRespawnDelaySeconds * 1000L;
                if (now >= deadline)
                {
                    Respawn(true);
                }
            }

            base.Update(elapsedTime);

            // we could implement some wrapper to step through all tickers, but writing them all explicitly here I think is more readable
            var pointsChanged = false;
            var elapsed = TimeSpan.FromMilliseconds(elapsedTime);
            pointsChanged |= _hpPassiveRestoreTicker.Step(elapsed);
            pointsChanged |= _hpRecoveryTicker.Step(elapsed);
            pointsChanged |= _spPassiveRestoreTicker.Step(elapsed);
            pointsChanged |= _spRecoveryTicker.Step(elapsed);
            pointsChanged |= _staminaFullRestoreTicker.Step(elapsed);
            pointsChanged |= _staminaConsumptionTicker.Step(elapsed);
            pointsChanged |= _affectsTicker.Step(elapsed);
            pointsChanged |= _flameSpiritHitTicker.Step(elapsed);
            pointsChanged |= _poisonTicker.Step(elapsed);
            pointsChanged |= _fireTicker.Step(elapsed);
            if (pointsChanged)
            {
                SendPoints();
            }

            _persistTime += (int)elapsedTime;
            if (_persistTime > SchedulingConstants.PersistInterval)
            {
                Persist().Wait(); // TODO
                _persistTime -= SchedulingConstants.PersistInterval;
            }
        }

        internal void MarkAttackedNow()
        {
            _lastAttackAt = GameServer.Instance.ServerTime;
            Affects.RemoveAllOfType(AffectType.From(EAffectType.InvisibleRespawn));
            Affects.RemoveAllOfType(AffectType.FromSkill(ESkill.Stealth));
        }

        public override EBattleType GetBattleType()
        {
            return EBattleType.Melee;
        }

        public override int GetMinDamage()
        {
            var weapon = Inventory.EquipmentWindow.Weapon;
            if (weapon == null) return 0;
            var item = _itemManager.GetItem(weapon.ItemId);
            if (item == null) return 0;
            return item.GetMinWeaponBaseDamage();
        }

        public override int GetMaxDamage()
        {
            var weapon = Inventory.EquipmentWindow.Weapon;
            if (weapon == null) return 0;
            var item = _itemManager.GetItem(weapon.ItemId);
            if (item == null) return 0;
            return item.GetMaxWeaponBaseDamage();
        }

        public override int GetBonusDamage()
        {
            var weapon = Inventory.EquipmentWindow.Weapon;
            if (weapon == null) return 0;
            var item = _itemManager.GetItem(weapon.ItemId);
            if (item == null) return 0;
            return item.GetAdditionalWeaponDamage();
        }

        public override void AddPoint(EPoint point, int value)
        {
            if (value == 0)
            {
                return;
            }

            switch (point)
            {
                case EPoint.Level:
                    LevelUp(value);
                    break;
                case EPoint.Experience:
                    if (_experienceManager.GetNeededExperience((byte)GetPoint(EPoint.Level)) == 0)
                    {
                        // we cannot add experience if no level up is possible
                        return;
                    }

                    var before = Player.Experience;
                    if (value < 0 && Player.Experience <= -value)
                    {
                        Player.Experience = 0;
                    }
                    else
                    {
                        Player.Experience = (uint)(Player.Experience + value);
                    }

                    if (value > 0)
                    {
                        var partialLevelUps = CalcPartialLevelUps(before, GetPoint(EPoint.Experience),
                            GetPoint(EPoint.NeededExperience));
                        if (partialLevelUps > 0)
                        {
                            Health = GetPoint(EPoint.MaxHp);
                            Mana = GetPoint(EPoint.MaxSp);
                            for (var i = 0; i < partialLevelUps; i++)
                            {
                                RecalculateStatusPoints();
                            }
                        }

                        CheckLevelUp();
                    }

                    break;
                case EPoint.Gold:
                    var gold = Player.Gold + value;
                    Player.Gold = (uint)Math.Min(uint.MaxValue, Math.Max(0, gold));
                    break;
                case EPoint.St:
                    Player.St = (byte)Math.Clamp(Player.St + value, 0, byte.MaxValue);
                    _stats.NotifyBaseChanged(EPoint.St);
                    break;
                case EPoint.Dx:
                    Player.Dx = (byte)Math.Clamp(Player.Dx + value, 0, byte.MaxValue);
                    _stats.NotifyBaseChanged(EPoint.Dx);
                    break;
                case EPoint.Ht:
                    Player.Ht = (byte)Math.Clamp(Player.Ht + value, 0, byte.MaxValue);
                    _stats.NotifyBaseChanged(EPoint.Ht);
                    break;
                case EPoint.Iq:
                    Player.Iq = (byte)Math.Clamp(Player.Iq + value, 0, byte.MaxValue);
                    _stats.NotifyBaseChanged(EPoint.Iq);
                    break;
                case EPoint.Hp:
                    Health = Math.Clamp(Health + value, 0, GetPoint(EPoint.MaxHp));
                    break;
                case EPoint.Sp:
                    Mana = Math.Clamp(Mana + value, 0, GetPoint(EPoint.MaxSp));
                    break;
                case EPoint.Stamina:
                    var oldStamina = Player.Stamina;
                    Player.Stamina = Math.Clamp(Player.Stamina + value, 0, GetPoint(EPoint.MaxStamina));

                    if (oldStamina == 0 && Player.Stamina > 0 && Mobility.IsForcedToWalk)
                    {
                        Mobility.ClearForceWalk(GameServer.Instance.ServerTime);
                    }
                    else if (oldStamina > 0 && Player.Stamina == 0)
                    {
                        Mobility.ForceWalk(GameServer.Instance.ServerTime);
                    }
                    break;
                case EPoint.StatusPoints:
                    // Debug.Assert(value > 0);
                    Player.AvailableStatusPoints = (uint)Math.Max(0, Player.AvailableStatusPoints + value);
                    break;
                case EPoint.Skill:
                    // Debug.Assert(value > 0);
                    Player.AvailableSkillPoints = (uint)Math.Max(0, Player.AvailableSkillPoints + value);
                    break;
                case EPoint.SubSkill:
                    // Debug.Assert(value > 0);
                    Player.AvailableSkillPoints = (uint)Math.Max(0, Player.AvailableSkillPoints + value);
                    break;
                case EPoint.PlayTime:
                    // Debug.Assert(value > 0);
                    Player.PlayTime = (ulong)Math.Max(0, (long)Player.PlayTime + value);
                    break;
                case EPoint.HpRecovery:
                    _hpRecoveryBucket = (uint)Math.Max(0, _hpRecoveryBucket + value);
                    break;
                case EPoint.SpRecovery:
                    _spRecoveryBucket = (uint)Math.Max(0, _spRecoveryBucket + value);
                    break;
                default:
                    _logger.LogError("Failed to add point to {Point}, unsupported", point);
                    break;
            }
        }

        internal static int CalcPartialLevelUps(uint before, uint after, uint requiredForNextLevel)
        {
            if (after >= requiredForNextLevel) return 0;

            const int CHUNK_AMOUNT = 4;
            var chunk = requiredForNextLevel / CHUNK_AMOUNT;
            var beforeChunk = (int)(before / (float)chunk);
            var afterChunk = (int)(after / (float)chunk);

            return afterChunk - beforeChunk;
        }

        public override void SetPoint(EPoint point, uint value)
        {
            switch (point)
            {
                case EPoint.Level:
                    var currentLevel = GetPoint(EPoint.Level);
                    LevelUp((int)value - (int)currentLevel);
                    break;
                case EPoint.Experience:
                    Player.Experience = value;
                    CheckLevelUp();
                    break;
                case EPoint.Gold:
                    Player.Gold = value;
                    break;
                case EPoint.PlayTime:
                    Player.PlayTime = value;
                    break;
                case EPoint.Skill:
                    Player.AvailableSkillPoints = (byte)value;
                    break;
                case EPoint.HpRecovery:
                    _hpRecoveryBucket = value;
                    break;
                case EPoint.SpRecovery:
                    _spRecoveryBucket = value;
                    break;
                case EPoint.Stamina:
                    var prevStamina = Player.Stamina;
                    Player.Stamina = Math.Clamp(value, 0, GetPoint(EPoint.MaxStamina));
                    if (prevStamina == 0 && Player.Stamina > 0 && Mobility.IsForcedToWalk)
                    {
                        Mobility.ClearForceWalk(GameServer.Instance.ServerTime);
                    }
                    else if (prevStamina > 0 && Player.Stamina == 0)
                    {
                        Mobility.ForceWalk(GameServer.Instance.ServerTime);
                    }
                    break;
                case EPoint.Hp:
                    if (value <= 0)
                    {
                        // 0 gets ignored by client
                        // Setting the Hp to 0 does not register as killing the player
                    }
                    else
                    {
                        Health = Math.Min(value, GetPoint(EPoint.MaxHp));
                    }
                    break;
                case EPoint.Sp:
                    if (value <= 0)
                    {
                        // 0 gets ignored by client
                    }
                    else
                    {
                        Mana = Math.Min(value, GetPoint(EPoint.MaxSp));
                    }
                    break;
                default:
                    _logger.LogError("Failed to set point to {Point}, unsupported", point);
                    break;
            }
        }

        private void Inventory_OnSlotChanged(object? sender, SlotChangedEventArgs args)
        {
            ApplyStatModifiersFromEquipment(args.Slot, args.ItemInstance);

            switch (args.Slot)
            {
                case EquipmentSlots.Weapon:
                    if (args.ItemInstance is null)
                    {
                        Affects.RemoveAllOfType(AffectType.FromSkill(ESkill.AuraOfTheSword));
                        Affects.RemoveAllOfType(AffectType.FromSkill(ESkill.EnchantedBlade));
                    }

                    break;
                case EquipmentSlots.Body:
                    if (Inventory.EquipmentWindow.Costume is null)  // costume overrides the normal armor (body) display
                    {
                        Player.BodyPart = args.ItemInstance?.ItemId ?? 0;
                    }
                
                    break;
                case EquipmentSlots.Costume:
                    // fallback to armor (body) display if unequipping costume
                    Player.BodyPart = args.ItemInstance?.ItemId ?? (Inventory.EquipmentWindow.Body?.ItemId ?? 0);
                    
                    break;
                case EquipmentSlots.Hair:
                    if (args.ItemInstance is not null)
                    {
                        var item = _itemManager.GetItem(args.ItemInstance.ItemId);
                        Player.HairPart = item.GetHairPartId();
                    }
                    else
                    {
                        Player.HairPart = 0;
                    }
                
                    break;
            }
        }

        private void ApplyStatModifiersFromEquipment(EquipmentSlots slot, ItemInstance? item)
        {
            var slotModifierKey = slot.AsModifierSource();

            _stats.RemoveAllModifiersWithSource(slotModifierKey);

            if (item is null)
            {
                // only unequipped, nothing else to do.
                return;
            }

            var proto = _itemManager.GetItem(item.ItemId);
            if (proto == null)
            {
                return;
            }

            foreach (var (applyType, value) in proto.Applies)
            {
                var point = applyType.ToPoint();
                // sanity checks
                if (point == EPoint.None || value == 0)
                {
                    continue;
                }

                _stats[point] += (value, slotModifierKey);
            }

            // TODO: Socket bonuses

            // TODO: Item attributes (switcher bonuses)

            if (proto.IsType(EItemType.Armor) && proto.IsSubtype(EArmorSubtype.Body, EArmorSubtype.Head, EArmorSubtype.Shield, EArmorSubtype.Shoes))
            {
                _stats[EPoint.DefenceGrade] += (proto.Values[1] + 2 * proto.Values[5], slotModifierKey);
            }

            if (proto.IsType(EItemType.Weapon) && !proto.IsSubtype(EWeaponSubtype.Arrow))
            {
                _stats[EPoint.MinWeaponDamage] += (proto.GetMinWeaponDamage(), slotModifierKey);
                _stats[EPoint.MaxWeaponDamage] += (proto.GetMaxWeaponDamage(), slotModifierKey);
            }
        }

        public override uint GetPoint(EPoint point)
        {
            // Handle static properties stored directly in Player or Entity
            switch (point)
            {
                case EPoint.Level:
                    return Player.Level;
                case EPoint.Experience:
                    return Player.Experience;
                case EPoint.NeededExperience:
                    return _experienceManager.GetNeededExperience(Player.Level);
                case EPoint.Hp:
                    return (uint)Health;
                case EPoint.Sp:
                    return (uint)Mana;
                case EPoint.Stamina:
                    return (uint)Player.Stamina;
                case EPoint.Gold:
                    return Player.Gold;
                case EPoint.StatusPoints:
                    return Player.AvailableStatusPoints;
                case EPoint.PlayTime:
                    return (uint)TimeSpan.FromMilliseconds(Player.PlayTime).TotalMinutes;
                case EPoint.Skill:
                    return Player.AvailableSkillPoints;
                case EPoint.SubSkill:
                    return 1;
                case EPoint.HpRecovery:
                    return _hpRecoveryBucket;
                case EPoint.SpRecovery:
                    return _spRecoveryBucket;
                
                // Special cases with bonus aggregation
                case EPoint.AttackGrade:
                    return (uint)(_stats[EPoint.AttackGrade] + _stats[EPoint.AttackGradeBonus]);
                case EPoint.DefenceGrade:
                    return (uint)(_stats[EPoint.DefenceGrade] + _stats[EPoint.DefenceGradeBonus]);
                case EPoint.MagicAttackGrade:
                    return (uint)(_stats[EPoint.MagicAttackGrade] + _stats[EPoint.MagicAttackGradeBonus]);
                case EPoint.MagicDefenceGrade:
                    return (uint)(_stats[EPoint.MagicDefenceGrade] + _stats[EPoint.MagicDefenceGradeBonus]);
                
                // All other cases delegate to the stat system
                default:
                    if (Enum.IsDefined(point)) 
                    {
                        return (uint)_stats[point];
                    }
                    
                    // _logger.LogWarning("Point {Point} is not implemented on player", point);
                    return 0;
            }
        }

        private async Task Persist()
        {
            await QuickSlotBar.Persist();

            Player.PositionX = PositionX;
            Player.PositionY = PositionY;
            Player.Health = Health;
            Player.Mana = Mana;
            Player.BodyPart = Inventory.EquipmentWindow.GetBodyPartId();
            Player.HairPart = Inventory.EquipmentWindow.GetHairPartId(_itemManager);

            await PersistAffectsAsync();
            await Skills.PersistAsync();

            var playerManager = _scope.ServiceProvider.GetRequiredService<IPlayerManager>();
            await playerManager.SetPlayerAsync(Player);
        }

        protected override void OnNewNearbyEntity(IEntity entity)
        {
            entity.ShowEntity(Connection);
        }

        protected override void OnRemoveNearbyEntity(IEntity entity)
        {
            entity.HideEntity(Connection);
        }

        public void DropItem(ItemInstance item, byte count)
        {
            if (count > item.Count)
            {
                return;
            }

            if (item.Count == count)
            {
                RemoveItem(item);
                SendRemoveItem(item.Window, (ushort)item.Position);
                _itemRepository.DeletePlayerItemAsync(_cacheManager, item.PlayerId, item.ItemId).Wait(); // TODO
            }
            else
            {
                item.Count -= count;
                item.Persist(_itemRepository).Wait(); // TODO

                SendItem(item);

                var proto = _itemManager.GetItem(item.ItemId);
                if (proto is null)
                {
                    _logger.LogCritical("Failed to find proto {ProtoId} for instanced item {ItemId}",
                        item.ItemId, item.Id);
                    return;
                }

                item = _itemManager.CreateItem(proto, count);
            }

            (Map as Map)?.AddGroundItem(item, PositionX, PositionY);
        }

        public void Pickup(IGroundItem groundItem)
        {
            if (Map is null) return;

            var item = groundItem.Item;
            if (item.ItemId == 1)
            {
                AddPoint(EPoint.Gold, (int)groundItem.Amount);
                SendPoints();
                Map.DespawnEntity(groundItem);

                return;
            }

            if (groundItem.OwnerName != null && !string.Equals(groundItem.OwnerName, Name))
            {
                SendChatInfo("This item is not yours");
                return;
            }

            if (!Inventory.PlaceItem(item).Result) // TODO
            {
                SendChatInfo("No inventory space left");
                return;
            }

            var itemName = _itemManager.GetItem(item.ItemId)?.TranslatedName ?? "Unknown";
            SendChatInfo($"You picked up {groundItem.Amount}x {itemName}");

            SendItem(item);
            Map.DespawnEntity(groundItem);
        }

        public void DropGold(uint amount)
        {
            var proto = _itemManager.GetItem(1);

            if (proto is null)
            {
                _logger.LogCritical("Cannot find proto for gold. This must never happen");
                return;
            }

            // todo prevent crashing the server with dropping gold too often ;)

            if (amount > GetPoint(EPoint.Gold))
            {
                return; // We can't drop more gold than we have ^^
            }

            AddPoint(EPoint.Gold, -(int)amount);
            SendPoints();

            var item = _itemManager.CreateItem(proto, 1); // count will be overwritten as it's gold
            (Map as Map)?.AddGroundItem(item, PositionX, PositionY,
                amount); // todo add method to IMap interface when we have an item interface...
        }

        /// <summary>
        /// Does nothing - if you want to persist the player use <see cref="OnDespawnAsync"/>
        /// </summary>
        public override void OnDespawn()
        {
        }

        public async Task OnDespawnAsync()
        {
            await Persist();
        }

        public int GetMobItemRate()
        {
            // todo: implement server rates, and premium server rates
            if (GetPremiumRemainSeconds(EPremiumTypes.Item) > 0)
                return 100;
            return 100;
        }

        public int GetPremiumRemainSeconds(EPremiumTypes type)
        {
            _logger.LogTrace("GetPremiumRemainSeconds not implemented yet");
            return 0; // todo: implement premium system
        }

        public bool IsUsableSkillMotion(int motion)
        {
            // todo: check if riding, mining or fishing
            return true;
        }

        public bool HasUniqueGroupItemEquipped(uint itemProtoId)
        {
            _logger.LogTrace("HasUniqueGroupItemEquipped not implemented yet");
            return false; // todo: implement unique group item system
        }

        public bool HasUniqueItemEquipped(uint itemProtoId)
        {
            {
                var item = Inventory.EquipmentWindow.GetItem(EquipmentSlots.Unique1);
                if (item != null && item.ItemId == itemProtoId)
                {
                    return true;
                }
            }
            {
                var item = Inventory.EquipmentWindow.GetItem(EquipmentSlots.Unique2);
                if (item != null && item.ItemId == itemProtoId)
                {
                    return true;
                }
            }

            return false;
        }

        public async Task CalculatePlayedTimeAsync()
        {
            var key = $"player:{Player.Id}:loggedInTime";
            var startSessionTime = await _cacheManager.Server.Get<long>(key);
            var totalSessionTime = Connection.Server.ServerTime - startSessionTime;
            if (totalSessionTime <= 0) return;

            AddPoint(EPoint.PlayTime, (int)totalSessionTime);
        }

        public ItemInstance? GetItem(byte window, ushort position)
        {
            switch (window)
            {
                case (byte)WindowType.Inventory:
                    if (position >= Inventory.Size)
                    {
                        // Equipment
                        return Inventory.EquipmentWindow.GetItem(position);
                    }
                    else
                    {
                        // Inventory
                        return Inventory.GetItem(position);
                    }
            }

            return null;
        }

        public bool IsSpaceAvailable(ItemInstance item, byte window, ushort position)
        {
            switch (window)
            {
                case (byte)WindowType.Inventory:
                    if (position >= Inventory.Size)
                    {
                        // Equipment
                        // Make sure item fits in equipment window
                        if (IsEquippable(item) && Inventory.EquipmentWindow.IsSuitable(_itemManager, item, position))
                        {
                            return Inventory.EquipmentWindow.GetItem(position) == null;
                        }

                        return false;
                    }
                    else
                    {
                        // Inventory
                        return Inventory.IsSpaceAvailable(item, position);
                    }
            }

            return false;
        }

        public bool IsEquippable(ItemInstance item)
        {
            var proto = _itemManager.GetItem(item.ItemId);
            if (proto == null)
            {
                // Proto for item not found
                return false;
            }

            if (proto.WearFlags == 0 && !proto.IsType(EItemType.Costume))
            {
                // No wear flags -> not wearable
                return false;
            }

            // Check anti flags
            var antiFlags = (EAntiFlags)proto.AntiFlags;
            if (antiFlags.HasFlag(AntiFlagClass))
            {
                return false;
            }

            if (antiFlags.HasFlag(AntiFlagGender))
            {
                SendChatInfo("This item is equippable only for the opposite gender.");
                return false;
            }

            // Check limits (level)
            foreach (var limit in proto.Limits)
            {
                if (limit.Type == (byte)ELimitType.Level)
                {
                    if (Player.Level < limit.Value)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool DestroyItem(ItemInstance item)
        {
            RemoveItem(item);
            if (!item.Destroy(_cacheManager).Result) // TODO
            {
                return false;
            }

            SendRemoveItem(item.Window, (ushort)item.Position);
            return true;
        }

        public void RemoveItem(ItemInstance item)
        {
            switch (item.Window)
            {
                case (byte)WindowType.Inventory:
                    if (item.Position >= Inventory.Size)
                    {
                        // Equipment
                        Inventory.RemoveEquipment(item);
                        SendCharacterUpdate();
                        SendPoints();
                    }
                    else
                    {
                        // Inventory
                        Inventory.RemoveItem(item);
                    }

                    break;
            }
        }

        public void SetItem(ItemInstance item, byte window, ushort position)
        {
            switch (window)
            {
                case (byte)WindowType.Inventory:
                    if (position >= Inventory.Size)
                    {
                        // Equipment
                        if (Inventory.EquipmentWindow.GetItem(position) == null)
                        {
                            Inventory.SetEquipment(item, position);
                            item.Set(_cacheManager, Player.Id, window, position, _itemRepository).Wait(); // TODO
                            SendCharacterUpdate();
                            SendPoints();
                        }
                    }
                    else
                    {
                        // Inventory
                        Inventory.PlaceItem(item, position);
                    }

                    break;
            }
        }

        public override void ShowEntity(IConnection connection)
        {
            SendGuildInfo();
            SendCharacter(connection);
            SendCharacterAdditional(connection);
            
            if (Mobility.IsCurrentlyWalking)
            {
                connection.Send(new SetMovingAnimation
                {
                    Vid = Vid,
                    Mode = (byte)SetMovingAnimation.MovingMode.Walking
                });
            }
        }

        public override void HideEntity(IConnection connection)
        {
            connection.Send(new RemoveCharacter {Vid = Vid});
            SendOfflineNotice(connection);
        }

        private void SendOfflineNotice(IConnection connection)
        {
            var guildId = Player.GuildId;
            if (guildId is not null && connection is IGameConnection gameConnection &&
                gameConnection.Player!.Player.GuildId == guildId)
            {
                connection.Send(new GuildMemberOfflinePacket {PlayerId = Player.Id});
            }
        }

        public void SendBasicData()
        {
            var details = new CharacterDetails
            {
                Vid = Vid,
                Name = Player.Name,
                Class = (ushort)Player.PlayerClass,
                PositionX = PositionX,
                PositionY = PositionY,
                Empire = Empire,
                SkillGroup = Player.SkillGroup
            };
            Connection.Send(details);
        }

        public void SendPoints()
        {
            var points = new CharacterPoints();
            for (var i = 0; i < points.Points.Length; i++)
            {
                points.Points[i] = GetPoint((EPoint)i);
            }

            Connection.Send(points);
        }

        public void SendInventory()
        {
            foreach (var item in Inventory.Items)
            {
                SendItem(item);
            }

            Inventory.EquipmentWindow.Send(this);
        }

        public void SendItem(ItemInstance item)
        {
            Debug.Assert(item.PlayerId == Player.Id);

            var p = new SetItem
            {
                Window = item.Window, Position = (ushort)item.Position, ItemId = item.ItemId, Count = item.Count
            };
            Connection.Send(p);
        }

        public void SendRemoveItem(byte window, ushort position)
        {
            Connection.Send(new SetItem {Window = window, Position = position, ItemId = 0, Count = 0});
        }

        public void SendCharacter(IConnection connection)
        {
            SyncSpeedsFromComputedStats();
            connection.Send(new SpawnCharacter
            {
                Vid = Vid,
                CharacterType = (byte)EEntityType.Player,
                Angle = 0,
                PositionX = PositionX,
                PositionY = PositionY,
                Class = (ushort)Player.PlayerClass,
                MoveSpeed = MovementSpeed,
                AttackSpeed = AttackSpeed,
                Affects = (ulong)Affects.GetActiveFlags()
            });
        }

        public void SendCharacterAdditional(IConnection connection)
        {
            connection.Send(new CharacterInfo
            {
                Vid = Vid,
                Name = Player.Name,
                Empire = Player.Empire,
                Level = Player.Level,
                GuildId = Guild?.Id ?? 0,
                Parts =
                [
                    (ushort)Inventory.EquipmentWindow.GetBodyPartId(),
                    (ushort)(Inventory.EquipmentWindow.Weapon?.ItemId ?? 0),
                    0,
                    (ushort)Inventory.EquipmentWindow.GetHairPartId(_itemManager)
                ]
            });
        }

        public void SendCharacterUpdate()
        {
            SyncSpeedsFromComputedStats();
            
            var packet = new CharacterUpdate
            {
                Vid = Vid,
                Parts =
                [
                    (ushort)Inventory.EquipmentWindow.GetBodyPartId(),
                    (ushort)(Inventory.EquipmentWindow.Weapon?.ItemId ?? 0),
                    0,
                    (ushort)Inventory.EquipmentWindow.GetHairPartId(_itemManager)
                ],
                MoveSpeed = MovementSpeed,
                AttackSpeed = AttackSpeed,
                GuildId = Guild?.Id ?? 0,
                Affects = (ulong)Affects.GetActiveFlags()
            };

            Connection.Send(packet);

            foreach (var entity in NearbyEntities)
            {
                if (entity is PlayerEntity p)
                {
                    p.Connection.Send(packet);
                }
            }
        }

        public void SendChatMessage(string message)
        {
            var chat = new ChatOutcoming
            {
                MessageType = ChatMessageTypes.Normal, Vid = Vid, Empire = Empire, Message = message
            };
            Connection.Send(chat);
        }

        public void SendChatCommand(string message)
        {
            var chat = new ChatOutcoming
            {
                MessageType = ChatMessageTypes.Command, Vid = 0, Empire = Empire, Message = message
            };
            Connection.Send(chat);
        }

        public void SendChatInfo(string message)
        {
            var chat = new ChatOutcoming
            {
                MessageType = ChatMessageTypes.Info, Vid = 0, Empire = Empire, Message = message
            };
            Connection.Send(chat);
        }

        private void OnMobilityModeChanged(EMobilityMode from, EMobilityMode to)
        {
            var mode = to switch
            {
                EMobilityMode.Walk => (byte)SetMovingAnimation.MovingMode.Walking,
                EMobilityMode.Run => (byte)SetMovingAnimation.MovingMode.Running,
                _ => throw new ArgumentOutOfRangeException(nameof(to))
            };

            var broadcast = new SetMovingAnimation { Vid = Vid, Mode = mode };
            Connection.Send(broadcast);
            foreach (var p in this.GetNearbyPlayers())
            {
                p.Connection.Send(broadcast);
            }
        }
        
        private void OnAffectAdded(EntityAffect affect)
        {
            if (affect.AffectFlag == EAffect.FlameSpirit)
            {
                _flameSpiritHitTicker.ArmFirstTickDelay();
            }

            var pointsChanged = false;
            if (affect.ModifiedPointId != EPoint.None && affect.ModifiedPointDelta != 0)
            {
                _stats[affect.ModifiedPointId] += (affect.ModifiedPointDelta, affect);
                pointsChanged = true;
            }

            Connection.Send(new AddAffect
            {
                AffectType = affect.AffectType.ToRaw(),
                ModifiedPointId = (byte)affect.ModifiedPointId,
                ModifiedPointDelta = affect.ModifiedPointDelta,
                AffectFlag = (uint)affect.AffectFlag,
                TotalDurationSec = (int)affect.RemainingDuration.TotalSeconds
            });

            SendCharacterUpdate();
            if (pointsChanged)
            {
                SendPoints();
            }
        }

        private void OnAffectRemoved(EntityAffect affect)
        {
            var pointsChanged = false;
            if (affect.ModifiedPointId != EPoint.None)
            {
                pointsChanged = _stats[affect.ModifiedPointId].RemoveModifier(affect);
            }
            
            Connection.Send(new RemoveAffect
            {
                AffectType = affect.AffectType.ToRaw(),
                ModifiedPointId = (byte)affect.ModifiedPointId
            });

            SendCharacterUpdate();
            if (pointsChanged)
            {
                SendPoints();
            }
        }

        private void SyncSpeedsFromComputedStats()
        {
            AttackSpeed = (byte)Math.Clamp(_stats[EPoint.AttackSpeed], 0, byte.MaxValue);
            MovementSpeed = (byte)Math.Clamp(_stats[EPoint.MoveSpeed], 0, byte.MaxValue);
        }

        private void SyncEquipmentModifiers()
        {
            foreach (var slot in Enum.GetValues<EquipmentSlots>())
            {
                var item = Inventory.EquipmentWindow.GetItem(slot);
                ApplyStatModifiersFromEquipment(slot, item);
            }
        }

        public void SendTarget()
        {
            var packet = new SetTarget();
            if (Target != null)
            {
                packet.TargetVid = Target.Vid;
                packet.Percentage = Target.HealthPercentage;
            }

            Connection.Send(packet);
        }

        public void Disconnect()
        {
            Inventory.OnSlotChanged -= Inventory_OnSlotChanged;
            Connection.Close();
        }

        public bool CancelCountdownEvent()
        {
            if (_countdownEventId.HasValue)
            {
                var wasScheduled = EventSystem.CancelEvent(_countdownEventId.Value);
                _countdownEventId = null;
                return wasScheduled;
            }
            return false;
        }

        public void SetCountdownEventCancellable(long eventId)
        {
            _countdownEventId = eventId;
        }

        public override string ToString()
        {
            return $"[Lv {GetPoint(EPoint.Level)}] {Player.Name} ({Vid})";
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}
