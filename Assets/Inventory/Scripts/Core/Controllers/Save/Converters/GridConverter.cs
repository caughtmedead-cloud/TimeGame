using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Items;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Inventory.Scripts.Core.Controllers.Save.Converters
{
    public class GridConverter : JsonConverter<GridTable>
    {
        public override void WriteJson(JsonWriter writer, GridTable value, JsonSerializer serializer)
        {
            var items = value.Slots.Cast<ItemTable>().Where(item => item != null).Distinct().ToList();

            var gridJObject = new JObject
            {
                { GetPropertyName(nameof(GridTable.Width)), value.Width },
                { GetPropertyName(nameof(GridTable.Height)), value.Height },
                { GetPropertyName(nameof(GridTable.Slots)), JArray.FromObject(items, serializer) }
            };

            serializer.Serialize(writer, gridJObject);
        }

        private static string GetPropertyName(string propNameOf)
        {
            return propNameOf.ToLower();
        }

        public override GridTable ReadJson(JsonReader reader, Type objectType, GridTable existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            // Deserialize the JSON array into a List<ItemTable>
            var grid = serializer.Deserialize<JObject>(reader);

            var width = GetValue(nameof(GridTable.Width), grid)?.Value<int>() ?? 0;
            var height = GetValue(nameof(GridTable.Height), grid)?.Value<int>() ?? 0;

            var itemList = GetValue(nameof(GridTable.Slots), grid)?.Value<JArray>()
                .ToObject<List<ItemTable>>(serializer) ?? new List<ItemTable>();


            var gridTable = new GridTable(width, height);

            foreach (var itemTable in itemList)
            {
                gridTable.PlaceItem(itemTable, itemTable.Position.x, itemTable.Position.y);
            }

            return gridTable;
        }

        private static JToken GetValue(string propertyName, JObject gridJObject)
        {
            return gridJObject.GetValue(propertyName, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}