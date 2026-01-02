using System.IO;
using UnityEngine;

namespace Inventory.Scripts.Core.Controllers.Save
{
    [CreateAssetMenu(menuName = "Inventory/Configuration/Save/Local Save Strategy")]
    public class LocalSaveStrategySo : SaveStrategySo
    {
        public override void Save(string key, string jsonEquippedItems)
        {
            var savePath = GetPath(key);

            File.WriteAllText(savePath, jsonEquippedItems);
        }

        public override string Load(string key)
        {
            var savePath = GetPath(key);

            if (File.Exists(savePath))
            {
                return File.ReadAllText(savePath);

                // var itemTables = JsonInventoryUtility.ParseJson(jsonData);

                // return itemTables;
            }

            Debug.LogWarning("No save file found. Path: " + savePath);
            return null;
        }

        public static string GetPath(string key)
        {
            var keyFixed = key.ToLower().Replace(" ", "_");

            return Path.Combine(Application.persistentDataPath, $"{keyFixed}.json");
        }
    }
}