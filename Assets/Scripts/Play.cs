using System;

[Serializable]
public class Play
{
    public string id;
    public string name;
    public long createdAtUtcTicks;

    public static Play Create(string name)
    {
        return new Play
        {
            id = Guid.NewGuid().ToString("N"),
            name = name.Trim(),
            createdAtUtcTicks = DateTime.UtcNow.Ticks
        };
    }
}
