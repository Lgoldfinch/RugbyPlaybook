using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class PlaybookStorage
{
    const string FileName = "plays.json";

    static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public List<Play> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new List<Play>();

            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json))
                return new List<Play>();

            var collection = JsonUtility.FromJson<PlayCollection>(json);
            if (collection?.items == null || collection.items.Length == 0)
                return new List<Play>();

            return collection.items.Where(p => p != null && !string.IsNullOrEmpty(p.id)).ToList();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"PlaybookStorage.Load failed: {e.Message}");
            return new List<Play>();
        }
    }

    public void Save(List<Play> plays)
    {
        try
        {
            var collection = new PlayCollection { items = plays.ToArray() };
            var json = JsonUtility.ToJson(collection, true);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"PlaybookStorage.Save failed: {e.Message}");
        }
    }
}
