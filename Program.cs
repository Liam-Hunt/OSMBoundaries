using OSMBoundaries;
using System.Text.Json;
using System.Xml;

Console.WriteLine("Hello, World!");

var boundaries = new List<Boundary>();

long count = 0;
var relation = false;
var inBoundary = false;
var name = "";
var id = "";
var ways = new List<NewWay>();
var tags = new Dictionary<string, string>();


var input = "E:/great-britain-latest.osm";
var output = "C:/Boundaries.json";


// Get Boundary relations
Console.WriteLine("Getting boundaries");

using (var reader = XmlReader.Create(input))
{
    while (reader.Read())
    {
        switch (reader.NodeType)
        {
            case XmlNodeType.Element:
                switch (reader.Name)
                {
                    case "relation":
                        relation = true;
                        id = reader.GetAttribute("id");
                        break;
                    case "tag":
                        if (relation)
                        {
                            switch (reader.GetAttribute("k"))
                            {
                                case "name":
                                    name = reader.GetAttribute("v");
                                    break;
                                case "type":
                                    inBoundary = reader.GetAttribute("v") == "boundary";
                                    break;
                                default:
                                    tags.TryAdd(reader.GetAttribute("k"), reader.GetAttribute("v"));
                                    break;
                            }
                        }
                        break;
                    case "member":
                        if (relation)
                        {
                            switch (reader.GetAttribute("type"))
                            {
                                case "way":
                                    ways.Add(new NewWay
                                    {
                                        Ref = reader.GetAttribute("ref"),
                                        Role = reader.GetAttribute("role")
                                    });
                                    break;
                            }
                        }
                        break;
                }
                break;
            case XmlNodeType.EndElement:
                switch (reader.Name)
                {
                    case "relation":
                        if (inBoundary)
                        {
                            boundaries.Add(new Boundary
                            {
                                Id = id,
                                Name = name,
                                Ways = ways,
                                Tags = tags
                            });
                        }

                        relation = false;
                        inBoundary = false;
                        name = "";
                        id = "";
                        ways = new List<NewWay>();
                        tags = new Dictionary<string, string>();
                        break;
                }
                break;
        }

        count++;

        if (count % 10000000 == 0)
        {
            Console.WriteLine(count);
        }
    }
}


// Get ways 
Console.WriteLine("Getting ways");

var neededWays = boundaries.SelectMany(x => x.Ways.Select(y => y.Ref)).Distinct().ToHashSet();
var wayLookup = boundaries.SelectMany(x => x.Ways).DistinctBy(x => x.Ref).ToDictionary(x => x.Ref, x => x);

count = 0;
var inWay = false;
var wayId = "";
var nds = new List<string>();
var foundWays = new List<Way>();

using (var reader = XmlReader.Create(input))
{
    while (reader.Read())
    {
        switch (reader.NodeType)
        {
            case XmlNodeType.Element:
                switch (reader.Name)
                {
                    case "way":
                        inWay = true;
                        wayId = reader.GetAttribute("id");
                        break;
                    case "nd":
                        if (inWay)
                        {
                            nds.Add(reader.GetAttribute("ref"));
                        }
                        break;
                }
                break;
            case XmlNodeType.EndElement:
                switch (reader.Name)
                {
                    case "way":
                        if (inWay && neededWays.Contains(wayId))
                        {
                            foundWays.Add(new Way
                            {
                                Id = wayId,
                                nds = nds
                            });
                        }

                        inWay = false;
                        wayId = "";
                        nds = new List<string>();
                        break;
                }
                break;
        }

        count++;

        if (count % 10000000 == 0)
        {
            Console.WriteLine(count);
        }
    }
}


// Get Nodes
Console.WriteLine("Getting nodes");

count = 0;
var neededNodes = foundWays.SelectMany(x => x.nds).Distinct().ToHashSet();
var nodes = new List<Node>();

using (var reader = XmlReader.Create(input))
{
    while (reader.Read())
    {
        switch (reader.NodeType)
        {
            case XmlNodeType.Element:
                switch (reader.Name)
                {
                    case "node":
                        if (neededNodes.Contains(reader.GetAttribute("id")))
                        {
                            var lat = reader.GetAttribute("lat");
                            var lon = reader.GetAttribute("lon");

                            if (!string.IsNullOrWhiteSpace(lat) && !string.IsNullOrWhiteSpace(lon))
                            {
                                nodes.Add(new Node
                                {
                                    Id = reader.GetAttribute("id"),
                                    Lat = double.Parse(lat),
                                    Long = double.Parse(lon)
                                });
                            }
                        }
                        break;
                }
                break;
        }

        count++;

        if (count % 10000000 == 0)
        {
            Console.WriteLine(count);
        }
    }
}



Console.BufferHeight = Int16.MaxValue - 1;

var nodeLookup = nodes.ToDictionary(x => x.Id, x => x);
var foundWayLookup = foundWays.ToDictionary(x => x.Id, x => x);

var finalBoundaries = new List<FinalBoundary>();

foreach (var boundary in boundaries)
{
    var finalBoundary = new FinalBoundary
    {
        Name = boundary.Name,
        Boundaries = new List<Bound>(),
        Tags = boundary.Tags,
    };


    // Gets confusing here because NewWays have the Role which Ways don't
    // And Ways have nds which NewWays don't
    // And Ways can be used by two different NewWays with different roles....

    var theseWays = boundary.Ways.Where(x => foundWayLookup.ContainsKey(x.Ref)).ToList();
    var actualWays = foundWayLookup.Where(x => theseWays.Any(y => y.Ref == x.Key)).Select(x => x.Value).ToList();

    var lastNodeId = "";

    var originalCount = theseWays.Count();

    for (int i = 0; i < originalCount; i++)
    {
        string targetWayId;
        List<string> targetNodes;

        if (i == 0)
        {
            targetWayId = theseWays.First().Ref;
            targetNodes = actualWays.FirstOrDefault(x => x.Id == targetWayId).nds;
            finalBoundary.Boundaries.Add(new Bound
            {
                Points = new List<Point>(),
                Type = theseWays.First().Role
            });
        }
        else
        {
            // Last node of first way is the first node of the next way
            var forwardsWay = actualWays.FirstOrDefault(x => x.nds.First() == lastNodeId);
            var backwardsWay = actualWays.FirstOrDefault(x => x.nds.Last() == lastNodeId);

            if (backwardsWay != null)
            {
                backwardsWay.nds.Reverse();

                targetWayId = backwardsWay.Id;
                targetNodes = backwardsWay.nds;
            }
            else if (forwardsWay != null)
            {
                targetWayId = forwardsWay.Id;
                targetNodes = forwardsWay.nds;
            }
            else
            {
                targetWayId = theseWays.First().Ref;
                targetNodes = actualWays.FirstOrDefault(x => x.Id == targetWayId).nds;
                finalBoundary.Boundaries.Add(new Bound
                {
                    Points = new List<Point>(),
                    Type = theseWays.First().Role
                });
            }
        }

        foreach (var nodeId in targetNodes)
        {
            var node = nodeLookup[nodeId];

            finalBoundary.Boundaries.Last().Points.Add(new Point { Lat = node.Lat, Lon = node.Long });

            lastNodeId = nodeId;
        }

        theseWays.Remove(theseWays.FirstOrDefault(x => x.Ref == targetWayId));
        actualWays.Remove(actualWays.FirstOrDefault(x => x.Id == targetWayId));
    }

    finalBoundaries.Add(finalBoundary);
}

File.WriteAllText(output, JsonSerializer.Serialize(finalBoundaries));