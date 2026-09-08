using InfernoProtocol.BetterUI;

int checks = 0;
void Check(bool pass, string message) { if (!pass) throw new Exception(message); checks++; }
Check(GeneTreeLayout.Navigate(0, 0, 1) == -1, "Empty collection must not select a node");
foreach (int count in new[] { 1, 2, 3, 4, 13, 40, 64 })
{
    for (int i = 0; i < count; i++)
    {
        var p = GeneTreeLayout.Position(i, count);
        Check(Math.Abs(p.X) + 90 < 300, "Caption must fit horizontal layout bounds");
        Check(Math.Abs(p.Y) <= GeneTreeLayout.Height(count) / 2, "Node outside scroll bounds");
        foreach (int dir in new[] { -2, -1, 1, 2 })
        {
            int target = GeneTreeLayout.Navigate(i, count, dir);
            Check(target >= 0 && target < count, "Selection out of range");
            var q = GeneTreeLayout.Position(target, count);
            Check(target == i || (dir == 2 ? q.Y < p.Y : dir == -2 ? q.Y > p.Y : dir == 1 ? q.X > p.X : q.X < p.X), "Navigation moved in wrong direction");
        }
        for (int j = i + 1; j < count; j++)
        {
            var q = GeneTreeLayout.Position(j, count);
            Check(Math.Abs(q.X - p.X) >= 180 || Math.Abs(q.Y - p.Y) >= 145, "Gene/caption bounds overlap");
        }
    }
    var visited = new HashSet<int> { 0 }; var queue = new Queue<int>(); queue.Enqueue(0);
    while (queue.TryDequeue(out int node)) foreach (int dir in new[] { -2, -1, 1, 2 })
    {
        int target = GeneTreeLayout.Navigate(node, count, dir);
        if (visited.Add(target)) queue.Enqueue(target);
    }
    Check(visited.Count == count, "Controller cannot reach every gene");
}
Console.WriteLine($"{checks} layout/navigation assertions passed (0–64 genes). Runtime visuals/input still require in-game testing.");
Check(!GeneLineGeometry.Normal(0,0,0,0,2,out _,out _), "Degenerate segment generated geometry");
Check(!GeneLineGeometry.Normal(0,0,1,1,0,out _,out _), "Zero width generated geometry");
for(int degrees=0;degrees<360;degrees+=5)
{
    float x=MathF.Cos(degrees*MathF.PI/180)*24, y=MathF.Sin(degrees*MathF.PI/180)*24;
    Check(GeneLineGeometry.Normal(0,0,x,y,2,out float nx,out float ny), "Valid segment rejected");
    Check(MathF.Abs(nx*nx+ny*ny-1)<.0001f,"Line width changed with orientation");
    Check(MathF.Abs(nx*x+ny*y)<.0001f,"Line offset is not perpendicular");
}
Console.WriteLine($"{checks} total assertions passed including batched line geometry.");
