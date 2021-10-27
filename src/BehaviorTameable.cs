using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Client;

namespace WolfTaming
{
    public enum DomesticationLevel
    {
        WILD, TAMING, DOMESTICATED
    }

    public class EntityBehaviorTameable : EntityBehavior
    {
        public DomesticationLevel domesticationLevel
        {
            get
            {
                DomesticationLevel level;
                if (Enum.TryParse<DomesticationLevel>(domesticationStatus.GetString("domesticationLevel"), out level))
                {
                    return level;
                }
                else
                {
                    return DomesticationLevel.WILD;
                }
            }
            set
            {
                domesticationStatus.SetString("domesticationLevel", value.ToString());
                entity.GetBehavior<EntityBehaviorTaskAIExtension>()?.reloadTasks();
                entity.WatchedAttributes.MarkPathDirty("domesticationLevel");
            }
        }

        public IPlayer owner
        {
            get
            {
                return entity.World.PlayerByUid(domesticationStatus.GetString("owner"));
            }
            set
            {
                domesticationStatus.SetString("owner", value.PlayerUID);
                entity.WatchedAttributes.MarkPathDirty("owner");
            }
        }

        public float domesticationProgress
        {
            get
            {
                switch (domesticationLevel)
                {
                    case DomesticationLevel.WILD: return 0f;
                    case DomesticationLevel.DOMESTICATED: return 1f;
                    case DomesticationLevel.TAMING: return domesticationStatus.GetFloat("progress");
                    default: return domesticationStatus.GetFloat("progress");
                }
            }
            set
            {
                domesticationStatus.SetFloat("progress", value);
                entity.WatchedAttributes.MarkPathDirty("progress");
            }
        }

        double cooldown
        {
            get
            {
                return domesticationStatus.GetDouble("cooldown", entity.World.Calendar.TotalHours);
            }
            set
            {
                domesticationStatus.SetDouble("cooldown", value);
                entity.WatchedAttributes.MarkPathDirty("cooldown");
            }
        }

        ITreeAttribute domesticationStatus
        {
            get
            {
                if (entity.WatchedAttributes.GetTreeAttribute("domesticationstatus") == null)
                {
                    entity.WatchedAttributes.SetAttribute("domesticationstatus", new TreeAttribute());
                }
                return entity.WatchedAttributes.GetTreeAttribute("domesticationstatus");
            }
            set
            {
                entity.WatchedAttributes.SetAttribute("domesticationstatus", value);
                entity.WatchedAttributes.MarkPathDirty("domesticationstatus");
            }
        }

        List<TamingItem> initiatorList = new List<TamingItem>();
        List<TamingItem> progressorList = new List<TamingItem>();
        public EntityBehaviorTameable(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            JsonObject[] initItems = attributes["initiator"]?.AsArray();
            if (initItems == null) initItems = new JsonObject[0];
            foreach (var item in initItems)
            {
                string name = item["code"].AsString();
                float progress = item["progress"].AsFloat();
                long cooldown = item["cooldown"].AsInt();

                initiatorList.Add(new TamingItem(name, progress, cooldown));
            }

            JsonObject[] progItems = attributes["progressor"]?.AsArray();
            if (progItems == null) progItems = new JsonObject[0];
            foreach (var item in progItems)
            {
                string name = item["code"].AsString();
                float progress = item["progress"].AsFloat();
                long cooldown = item["cooldown"].AsInt();

                progressorList.Add(new TamingItem(name, progress, cooldown));
            }
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
            EntityPlayer player = byEntity as EntityPlayer;
            if (player == null) return;
            if (owner != null && owner != player.Player) return;
            if (domesticationLevel == DomesticationLevel.WILD
                && itemslot?.Itemstack?.Item != null)
            {
                var tamingItem = initiatorList.Find((item) => itemslot.Itemstack.Item.Code.ToString().Contains(item.name));
                if (tamingItem != null)
                {
                    itemslot.TakeOut(1);
                    cooldown = entity.World.Calendar.TotalHours + tamingItem.cooldown;
                    domesticationLevel = DomesticationLevel.TAMING;
                    owner = player.Player;
                    domesticationProgress = tamingItem.progress;
                    ICoreClientAPI capi = entity.Api as ICoreClientAPI;
                    if (capi != null)
                    {
                        capi.ShowChatMessage(String.Format("Successfully startet taming {0}, current progress is {1}%.", entity.GetName(), domesticationProgress * 100));
                    }
                }
            }
            else if (domesticationLevel == DomesticationLevel.TAMING
                && itemslot?.Itemstack?.Item != null)
            {
                if (cooldown <= entity.World.Calendar.TotalHours)
                {
                    var tamingItem = progressorList.Find((item) => itemslot.Itemstack.Collectible.Code.ToString().Contains(item.name));
                    if (tamingItem != null)
                    {
                        itemslot.TakeOut(1);
                        cooldown = entity.World.Calendar.TotalHours + tamingItem.cooldown;
                        domesticationProgress += tamingItem.progress;
                        ICoreClientAPI capi = entity.Api as ICoreClientAPI;
                        if (capi != null)
                        {
                            capi.ShowChatMessage(String.Format("Continued taming {0}, current progress is {1}%.", entity.GetName(), domesticationProgress * 100));
                        }
                    }
                }
                else
                {
                    ICoreClientAPI capi = entity.Api as ICoreClientAPI;
                    if (capi != null)
                    {
                        capi.ShowChatMessage(String.Format("Entity {0} is not ready to be tended to again.", entity.GetName()));
                    }
                }
            }
            if (domesticationProgress >= 1f)
            {
                domesticationLevel = DomesticationLevel.DOMESTICATED;
                ICoreClientAPI capi = entity.Api as ICoreClientAPI;
                if (capi != null)
                {
                    capi.ShowChatMessage(String.Format("Successfully tamed {0}.", entity.GetName()));
                }
            }
        }

        public override string PropertyName()
        {
            return "tameable";
        }
    }

    class TamingItem
    {
        public string name { get; }
        public float progress { get; }
        public long cooldown { get; }

        public TamingItem(string name, float progress, long cooldown)
        {
            this.name = name;
            this.progress = progress;
            this.cooldown = cooldown;
        }
    }
}