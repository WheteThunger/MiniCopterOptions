using System;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins {
    [Info("Mini-Copter Options", "Pho3niX90", "1.1.6")]
    [Description("Provide a number of additional options for Mini-Copters, including storage and seats.")]
    class MiniCopterOptions : RustPlugin {
        static MiniCopterOptions _instance;
        #region Prefab Modifications

        private readonly string storagePrefab = "assets/prefabs/deployable/hot air balloon/subents/hab_storage.prefab";
        private readonly string storageLargePrefab = "assets/content/vehicles/boats/rhib/subents/rhib_storage.prefab";
        private readonly string autoturretPrefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab";
        private readonly string switchPrefab = "assets/prefabs/deployable/playerioents/simpleswitch/switch.prefab";
        void Loaded() {
            _instance = this;
        }
        void OnEntityDismounted(BaseMountable entity, BasePlayer player) {
            if (config.flyHackPause > 0 && entity is MiniCopter)
                player.PauseFlyHackDetection(config.flyHackPause);
        }

        void AddLargeStorageBox(MiniCopter copter) {
            //sides,negative left | up and down
            if (config.storageLargeContainers == 1) {
                AddStorageBox(copter, storageLargePrefab, new Vector3(0.0f, 0.07f, -1.05f), Quaternion.Euler(0, 180f, 0));
            } else if (config.storageLargeContainers >= 2) {
                AddStorageBox(copter, storageLargePrefab, new Vector3(-0.48f, 0.07f, -1.05f), Quaternion.Euler(0, 180f, 0));
                AddStorageBox(copter, storageLargePrefab, new Vector3(0.48f, 0.07f, -1.05f), Quaternion.Euler(0, 180f, 0));
            }
        }

        void AddRearStorageBox(MiniCopter copter) {
            AddStorageBox(copter, storagePrefab, new Vector3(0, 0.75f, -1f));
        }

        void AddSideStorageBoxes(MiniCopter copter) {
            AddStorageBox(copter, storagePrefab, new Vector3(0.6f, 0.24f, -0.35f));
            AddStorageBox(copter, storagePrefab, new Vector3(-0.6f, 0.24f, -0.35f));
        }

        void AddStorageBox(MiniCopter copter, string prefab, Vector3 position) {
            AddStorageBox(copter, prefab, position, new Quaternion());
        }

        void AddStorageBox(MiniCopter copter, string prefab, Vector3 position, Quaternion q) {

            StorageContainer box = GameManager.server.CreateEntity(prefab, copter.transform.position, q) as StorageContainer;

            if (prefab.Equals(storageLargePrefab) && config.largeStorageLockable) {
                box.isLockable = true;
                box.panelName = GetPanelName(config.largeStorageSize);
            }

            box.Spawn();
            box.SetParent(copter);
            box.transform.localPosition = position;

            if (prefab.Equals(storageLargePrefab) && config.largeStorageLockable) {
                box.inventory.capacity = config.largeStorageSize;
            }


            box.SendNetworkUpdateImmediate(true);
        }

        void AddTurret(MiniCopter copter) {
            AutoTurret aturret = GameManager.server.CreateEntity(autoturretPrefab, copter.transform.position) as AutoTurret;
            aturret.Spawn();
            aturret.pickup.enabled = false;
            aturret.sightRange = config.turretRange;
            DestroyGroundComp(aturret);
            aturret.SetParent(copter);
            aturret.transform.localPosition = new Vector3(0, 0, 2.47f);
            aturret.transform.localRotation = Quaternion.Euler(0, 0, 0);
            ProtoBuf.PlayerNameID pnid = new ProtoBuf.PlayerNameID();
            if (copter.OwnerID != 0) {
                pnid.userid = copter.OwnerID;
                pnid.username = BasePlayer.FindByID(copter.OwnerID)?.displayName;
                aturret.authorizedPlayers.Add(pnid);
            }
            aturret.SendNetworkUpdate();

            timer.Once(1.0f, () => {
                ElectricSwitch aSwitch = aturret.GetComponentInChildren<ElectricSwitch>();
                aSwitch = GameManager.server.CreateEntity(switchPrefab, aturret.transform.position)?.GetComponent<ElectricSwitch>();
                if (aSwitch == null) return;
                aSwitch.pickup.enabled = false;
                aSwitch.SetParent(aturret);
                aSwitch.transform.localPosition = new Vector3(0f, -0.65f, 0.325f);
                aSwitch.transform.localRotation = Quaternion.Euler(0, 0, 0);
                aSwitch.Spawn();
                aSwitch._limitedNetworking = false;
                DestroyGroundComp(aSwitch);
                aturret.inputs[0].connectedTo.Set(aSwitch);
                aturret.inputs[0].connectedToSlot = 0;
                aturret.inputs[0].connectedTo.Init();
                aSwitch.outputs[0].connectedTo.Set(aturret);
                aSwitch.outputs[0].connectedToSlot = 0;
                aSwitch.outputs[0].connectedTo.Init();
                aSwitch.MarkDirtyForceUpdateOutputs();
                aSwitch.UpdateHasPower(12, 0);
                aSwitch.SendNetworkUpdate();
            });
        }

        void OnItemDeployed(Deployer deployer, BaseEntity entity) {
            if (entity?.GetParentEntity() != null && entity.GetParentEntity().ShortPrefabName.Equals("minicopter.entity")) {
                CodeLock cLock = entity.GetComponentInChildren<CodeLock>();
                cLock.transform.localPosition = new Vector3(0.0f, 0.3f, 0.298f);
                cLock.transform.localRotation = Quaternion.Euler(new Vector3(0, 90, 0));
                cLock.SendNetworkUpdateImmediate();
            }
        }

        void DestroyGroundComp(BaseEntity ent) {
            UnityEngine.Object.Destroy(ent.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.Destroy(ent.GetComponent<GroundWatch>());
        }


        private object OnSwitchToggle(ElectricSwitch eswitch, BasePlayer player) {
            BaseEntity parent = eswitch.GetParentEntity();
            if (parent != null && parent.PrefabName.Equals(autoturretPrefab)) {
                AutoTurret turret = parent as AutoTurret;
                if (turret == null) return null;
                if (!eswitch.IsOn())
                    PowerTurretOn(turret);
                else
                    PowerTurretOff(turret);
            }
            return null;
        }

        public void PowerTurretOn(AutoTurret turret) {
            turret.SetFlag(BaseEntity.Flags.Reserved8, true);
            turret.InitiateStartup();
        }
        private void PowerTurretOff(AutoTurret turret) {
            turret.SetFlag(BaseEntity.Flags.Reserved8, false);
            turret.InitiateShutdown();
        }

        string GetPanelName(int capacity) {
            if (capacity <= 6) {
                return "smallstash";
            } else if (capacity > 6 && capacity <= 12) {
                return "smallwoodbox";
            } else if (capacity > 12 && capacity <= 30) {
                return "largewoodbox";
            } else {
                return "genericlarge";
            }
        }

        void RestoreMiniCopter(MiniCopter copter, bool removeStorage = false) {
            copter.fuelPerSec = copterDefaults.fuelPerSecond;
            copter.liftFraction = copterDefaults.liftFraction;
            copter.torqueScale = copterDefaults.torqueScale;
            if (removeStorage) {
                foreach (var child in copter.children.ToList()) {
                    if (child.name == storagePrefab || child.name == storageLargePrefab || child.name == autoturretPrefab) {
                        copter.RemoveChild(child);
                        child.Kill();
                    }
                }
            }
        }

        void ModifyMiniCopter(MiniCopter copter, bool storage = false) {
            copter.fuelPerSec = config.fuelPerSec;
            copter.liftFraction = config.liftFraction;
            copter.torqueScale = new Vector3(config.torqueScalePitch, config.torqueScaleYaw, config.torqueScaleRoll);

            if (config.autoturret && copter.GetComponentInChildren<AutoTurret>() == null) {
                AddTurret(copter);
            }
            if (storage) AddLargeStorageBox(copter);
            if (storage)
                switch (config.storageContainers) {
                    case 1:
                        AddRearStorageBox(copter);
                        break;
                    case 2:
                        AddSideStorageBoxes(copter);
                        break;
                    case 3:
                        AddRearStorageBox(copter);
                        AddSideStorageBoxes(copter);
                        break;
                }
        }

        void StoreMiniCopterDefaults(MiniCopter copter) {
            copterDefaults = new MiniCopterDefaults {
                fuelPerSecond = copter.fuelPerSec,
                liftFraction = copter.liftFraction,
                torqueScale = copter.torqueScale
            };
        }

        #endregion
        #region Hooks

        void OnServerInitialized() {
            PrintWarning("Applying settings except storage modifications to existing MiniCopters.");
            foreach (var copter in UnityEngine.Object.FindObjectsOfType<MiniCopter>()) {
                // Nab the default values off the first minicopter.
                if (copterDefaults.Equals(default(MiniCopterDefaults))) {
                    StoreMiniCopterDefaults(copter);
                    break;
                }
                if (config.landOnCargo) copter.gameObject.AddComponent<MiniShipLandingGear>();

                //ModifyMiniCopter(copter, config.reloadStorage);
            }
        }

        void OnEntitySpawned(MiniCopter mini) {

            // Only add storage on spawn so we don't stack or mess with
            // existing player storage containers. 
            ModifyMiniCopter(mini, true);
            if (config.landOnCargo) mini.gameObject.AddComponent<MiniShipLandingGear>();

        }

        void OnEntityKill(BaseNetworkable entity) {
            if (!config.dropStorage || !entity.ShortPrefabName.Equals("minicopter.entity")) return;
            StorageContainer[] containers = entity.GetComponentsInChildren<StorageContainer>();
            foreach (StorageContainer container in containers) {
                container.DropItems();
            }
            AutoTurret[] turrets = entity.GetComponentsInChildren<AutoTurret>();
            foreach (AutoTurret turret in turrets) {
                turret.DropItems();
            }
        }

        void Unload() {
            foreach (var copter in UnityEngine.Object.FindObjectsOfType<MiniCopter>()) {
                RestoreMiniCopter(copter, config.reloadStorage);
                if (config.landOnCargo) UnityEngine.Object.Destroy(copter.GetComponent<MiniShipLandingGear>());
            }
        }



        #endregion
        #region Configuration

        private class MiniCopterOptionsConfig {
            // Populated with Rust defaults.
            public float fuelPerSec = 0.25f;
            public float liftFraction = 0.25f;
            public float torqueScalePitch = 400f;
            public float torqueScaleYaw = 400f;
            public float torqueScaleRoll = 200f;

            public int storageContainers = 0;
            public int storageLargeContainers = 0;
            public bool restoreDefaults = true;
            public bool reloadStorage = false;
            public bool dropStorage = true;
            public bool largeStorageLockable = true;
            public int largeStorageSize = 42;
            public int flyHackPause = 4;
            public bool autoturret = true;
            public bool landOnCargo = true;
            public float turretRange = 30f;

            // Plugin reference
            private MiniCopterOptions plugin;

            public MiniCopterOptionsConfig(MiniCopterOptions plugin) {
                this.plugin = plugin;

                GetConfig(ref fuelPerSec, "Fuel per Second");
                GetConfig(ref liftFraction, "Lift Fraction");
                GetConfig(ref torqueScalePitch, "Pitch Torque Scale");
                GetConfig(ref torqueScaleYaw, "Yaw Torque Scale");
                GetConfig(ref torqueScaleRoll, "Roll Torque Scale");
                GetConfig(ref storageContainers, "Storage Containers");
                GetConfig(ref storageLargeContainers, "Large Storage Containers");
                GetConfig(ref restoreDefaults, "Restore Defaults");
                GetConfig(ref reloadStorage, "Reload Storage");
                GetConfig(ref dropStorage, "Drop Storage Loot On Death");
                GetConfig(ref largeStorageLockable, "Large Storage Lockable");
                GetConfig(ref largeStorageSize, "Large Storage Size (Max 42)");
                GetConfig(ref flyHackPause, "Seconds to pause flyhack when dismount from heli.");
                GetConfig(ref autoturret, "Add auto turret to heli");
                GetConfig(ref landOnCargo, "Allow Minis to Land on Cargo");
                GetConfig(ref turretRange, "Mini Turret Range (Default 30)");

                plugin.SaveConfig();
            }

            private void GetConfig<T>(ref T variable, params string[] path) {
                if (path.Length == 0) return;

                if (plugin.Config.Get(path) == null) {
                    SetConfig(ref variable, path);
                    plugin.PrintWarning($"Added field to config: {string.Join("/", path)}");
                }

                variable = (T)Convert.ChangeType(plugin.Config.Get(path), typeof(T));
            }

            private void SetConfig<T>(ref T variable, params string[] path) => plugin.Config.Set(path.Concat(new object[] { variable }).ToArray());
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file.");

        private MiniCopterOptionsConfig config;

        struct MiniCopterDefaults {
            public float fuelPerSecond;
            public float liftFraction;
            public Vector3 torqueScale;
        }

        MiniCopterDefaults copterDefaults;

        private void Init() {
            config = new MiniCopterOptionsConfig(this);

            if (config.storageContainers > 3) {
                PrintWarning($"Storage Containers configuration value {config.storageContainers} exceeds the maximum, setting to 3.");
                config.storageContainers = 3;
            } else if (config.storageContainers < 0) {
                PrintWarning($"Storage Containers cannot be a negative value, setting to 0.");
                config.storageContainers = 0;
            }

            if (config.storageLargeContainers > 2) {
                PrintWarning($"Large Storage Containers configuration value {config.storageLargeContainers} exceeds the maximum, setting to 2.");
                config.storageLargeContainers = 2;
            } else if (config.storageLargeContainers < 0) {
                PrintWarning($"Large Storage Containers cannot be a negative value, setting to 0.");
                config.storageLargeContainers = 0;
            }

            if (config.largeStorageSize > 42) {
                PrintWarning($"Large Storage Containers Capacity configuration value {config.largeStorageSize} exceeds the maximum, setting to 42.");
                config.largeStorageSize = 42;
            } else if (config.largeStorageSize < 6) {
                PrintWarning($"Storage Containers Capacity cannot be a smaller than 6, setting to 6.");
            }
        }

        #endregion

        #region Classes
        public class MiniShipLandingGear : MonoBehaviour {
            private MiniCopter miniCopter;
            private uint cargoId;
            private bool isDestroyed = false;

            void Awake() {
                miniCopter = gameObject.GetComponent<MiniCopter>();
            }

            void OnTriggerEnter(Collider col) {
                _instance.PrintToChat("Enter");
                if (cargoId > 0) {
                    CancelInvoke("Exit");
                    return;
                }

                if (!string.Equals(col.gameObject.name, "trigger")) return;

                CargoShip cargo = col.ToBaseEntity() as CargoShip;
                if (cargo == null) return;

                cargoId = cargo.net.ID;
                miniCopter.SetParent(cargo, true);
            }

            void OnTriggerExit(Collider col) {
                _instance.PrintToChat("Exit");
                if (isDestroyed || cargoId == 0 || col.ToBaseEntity().net.ID != cargoId) return;
                Invoke("Exit", 1.5f);
            }

            void Exit() {
                cargoId = 0;
                if (isDestroyed || miniCopter == null || miniCopter.IsDestroyed || !miniCopter.IsMounted()) return;
                miniCopter.SetParent(null, true);
            }

            void OnDestroy() {
                isDestroyed = true;
                CancelInvoke("Exit");

                if (miniCopter == null || miniCopter.IsDestroyed) return;
                miniCopter.SetParent(null, true, true);
            }
        }
        #endregion

    }
}
