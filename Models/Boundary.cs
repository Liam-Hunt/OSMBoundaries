namespace OSMBoundaries
{
    internal class Boundary
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public List<NewWay> Ways { get; set; }

        public Dictionary<string,string> Tags { get; set; }
    }
}
