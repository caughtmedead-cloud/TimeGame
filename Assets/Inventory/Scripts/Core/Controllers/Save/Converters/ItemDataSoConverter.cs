using System;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using Newtonsoft.Json;

namespace Inventory.Scripts.Core.Controllers.Save.Converters
{
    public class ItemDataSoConverter : JsonConverter<ItemDataSo>
    {
        public override void WriteJson(JsonWriter writer, ItemDataSo value, JsonSerializer serializer)
        {
            writer.WriteValue($"{value.uniqueUid.Uid}");
        }

        public override ItemDataSo ReadJson(JsonReader reader, Type objectType, ItemDataSo existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            var uid = serializer.Deserialize<string>(reader);

            return StaticInventoryContext.GetItemDataFromId(uid);
        }
    }
}