namespace GWYF_CasinoChaos
{
    // Positions are in Model/Base's inspected local XY plane. Two-sided cloned
    // models retain their original Z/rotation. Capacity: five paired rows.
    internal static class BodyMachineLayout
    {
        internal const int Columns = 2, Capacity = 10;
        internal const float Scale = .55f, ColumnSpacing = .32f, RowSpacing = .34f, Top = 3.55f;
        internal const float CenterOffset = -.14f;
        internal static float X(int order, float center) => center + CenterOffset + ((order % Columns) - .5f) * ColumnSpacing;
        internal static float Y(int order) => Top - (order / Columns) * RowSpacing;
    }
}
