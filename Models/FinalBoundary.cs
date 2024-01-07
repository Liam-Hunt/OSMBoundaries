namespace OSMBoundaries
{
    public class FinalBoundary
    {
        public string Name { get; set; }

        // Need a list of list because some boundaries have seperate islands not connected to main boundary
        public List<Bound> Boundaries { get; set; }

        public Dictionary<string,string> Tags { get; set; }
    }

    public class Bound
    {
        public List<Point> Points { get; set; }

        public string Type { get; set; }
    }
}
