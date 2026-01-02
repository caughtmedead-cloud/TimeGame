using System;
using System.Collections.Generic;
using Inventory.Scripts.Core.Environment;
using Inventory.Scripts.Core.Holders;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Inventory.Scripts.Core.Controllers.Save.Converters
{
    public class HolderConverter : JsonConverter<HolderData>
    {
        private static readonly List<Type> HoldersToSerializeTransform = new()
        {
            typeof(EnvironmentItemHolder),
            typeof(EnvironmentContainerHolder)
        };

        private const string IdPropertyName = "id";
        private const string HolderTypePropertyName = "type";
        private const string ItemDataSoPropertyName = "item_data_so";
        private const string TransformPropertyName = "transform";

        public override void WriteJson(JsonWriter writer, HolderData value, JsonSerializer serializer)
        {
            var primaryHolder = value.GetPrimaryHolder();

            var holderJObject = new JObject
            {
                { IdPropertyName, value.id }
            };

            if (primaryHolder != null)
            {
                holderJObject.Add(HolderTypePropertyName, primaryHolder.GetType().Name);
            }

            AddItemDataIfNeeded(holderJObject, primaryHolder);

            serializer.Serialize(writer, holderJObject);
        }

        private static void AddItemDataIfNeeded(JObject holderJObject, Holder primaryHolder)
        {
            if (primaryHolder == null) return;

            if (!HoldersToSerializeTransform.Contains(primaryHolder.GetType())) return;

            var equippedItem = primaryHolder.GetItemEquipped();

            var itemDataSoId = JsonConvert.SerializeObject(
                equippedItem.ItemDataSo,
                JsonInventoryUtility.GetJsonSerializerSettings()
            ).Replace("\"", "");

            holderJObject.Add(ItemDataSoPropertyName, itemDataSoId);

            var holderTransform = primaryHolder.transform;
            var serializedTransform = new SerializedTransform
            {
                pos = holderTransform.position,
                rot = holderTransform.rotation,
                scale = holderTransform.localScale
            };

            holderJObject.Add(TransformPropertyName, JsonConvert.SerializeObject(
                serializedTransform,
                JsonInventoryUtility.GetJsonSerializerSettings()
            ));
        }

        public override HolderData ReadJson(JsonReader reader, Type objectType, HolderData existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            var jObject = serializer.Deserialize<JObject>(reader);

            var id = jObject.Value<string>(IdPropertyName);
            var type = jObject.Value<string>(HolderTypePropertyName);

            if (IsNotEnvironmentHolder(type))
            {
                return new HolderData(id);
            }

            var holderFromCurrentScene = StaticInventoryContext.GetHolderFromId(id, null);

            return IsAExistentHolder(holderFromCurrentScene, type)
                ? UpdateHolderPosition(holderFromCurrentScene, jObject)
                : InstantiateHolder(jObject);
        }

        private static bool IsNotEnvironmentHolder(string type)
        {
            // Replicators will pass through here because it's not an environment holder. Only the character body is a object in the world.
            if (type == null)
            {
                return true;
            }

            return HoldersToSerializeTransform.Find(holderType => holderType.Name == type) == null;
        }

        // This will be true for prefab items that already exists the scene or container like chest, ATMs and any kind of static container.
        private static bool IsAExistentHolder(Holder holderFromId, string type)
        {
            return holderFromId != null && nameof(EnvironmentContainerHolder) == type;
        }

        private static HolderData UpdateHolderPosition(Holder holderFromCurrentScene,
            JToken token)
        {
            var holderTransform =
                JsonConvert.DeserializeObject<SerializedTransform>(token.Value<string>(TransformPropertyName));

            var transform = holderFromCurrentScene.transform;
            transform.position = holderTransform.pos;
            transform.rotation = holderTransform.rot;
            transform.localScale = holderTransform.scale;

            return holderFromCurrentScene.GetHolderData();
        }

        private static HolderData InstantiateHolder(JObject jObject)
        {
            var holderTransform =
                JsonConvert.DeserializeObject<SerializedTransform>(jObject.Value<string>(TransformPropertyName));
            var itemDataSo =
                JsonConvert.DeserializeObject<ItemDataSo>($"\"{jObject.Value<string>(ItemDataSoPropertyName)}\"");

            return InstantiateDroppedItem(itemDataSo, holderTransform);
        }

        private static HolderData InstantiateDroppedItem(ItemDataSo itemDataSo, SerializedTransform holderTransform)
        {
            if (itemDataSo == null)
            {
                return null;
            }

            return StaticInventoryContext.InstantiateItemInTheWorld(
                itemDataSo,
                holderTransform.pos,
                holderTransform.rot,
                holderTransform.scale
            )?.GetHolderData();
        }

        [Serializable]
        private class SerializedTransform
        {
            public Vector3 pos;
            public Quaternion rot;
            public Vector3 scale;
        }
    }
}