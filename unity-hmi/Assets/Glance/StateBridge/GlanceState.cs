namespace Glance
{
    public static class GlanceState
    {
        public static string StationId { get; private set; } = "radio_1";
        public static void SelectStation(string id)
        {
            if (id != "radio_1" && id != "radio_2" && id != "radio_3")
                throw new System.ArgumentException("Unknown station");
            StationId = id;
        }
        public static void Reset() => StationId = "radio_1";
    }
}
