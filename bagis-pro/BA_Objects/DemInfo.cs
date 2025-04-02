using System;

namespace bagis_pro.BA_Objects
{
    public class DemInfo
    {
        public string idText { get; set; }
        public double cellSize { get; set; }
        public double min { get; set; }
        public double max { get; set; }
        public double range { get; set; }
        public bool exists { get; set; }
        public double x_CellSize { get; set; }
        public double y_CellSize { get; set; }
        public double widthInPixels { get; set; }
        public double heightInPixels { get; set; }
        public double minMapX { get; set; }
        public double maxMapX { get; set; }
        public double minMapY { get; set; }
        public double maxMapY { get; set; }
    }
}
