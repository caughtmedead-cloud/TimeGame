using System.Collections.Generic;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.Items.Metadata;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Inventory.Scripts.Core.Controllers.Save
{
    public static class JsonInventoryUtility
    {
        private const Formatting JsonFormatting = Formatting.Indented;

        private static readonly DefaultContractResolver ContractResolver = new()
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        };

        public static string GetJson(List<ItemTable> inventories)
        {
            Debug.Log($"Storing data to a JSON object...".SaveSystem());

            return JsonConvert.SerializeObject(inventories, JsonFormatting, GetJsonSerializerSettings());
        }

        public static List<ItemTable> ParseJson(string jsonData)
        {
            Debug.Log($"Loading JSON save data...".SaveSystem());

            var itemTables = JsonConvert.DeserializeObject<List<ItemTable>>(jsonData, GetJsonSerializerSettings());

            SetItemProps(itemTables);

            return itemTables;
        }

        private static void SetItemProps(List<ItemTable> itemTables)
        {
            foreach (var itemTable in itemTables)
            {
                if (!itemTable.IsContainer()) continue;

                var containerMetadata = itemTable.GetMetadata<ContainerMetadata>();

                var allItems = containerMetadata.GetAllItems();

                SetItemProps(allItems);
            }
        }

        public static JsonSerializerSettings GetJsonSerializerSettings()
        {
            return new JsonSerializerSettings
            {
                ContractResolver = ContractResolver,
                TypeNameHandling = TypeNameHandling.Auto,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
                // PreserveReferencesHandling = PreserveReferencesHandling.Objects
            };
        }
    }
}