using EggSharp.ProtoParity;

if (args.Length != 3) {
    await Console.Error.WriteLineAsync("usage: ProtoParity <base.proto> <head.proto> <roots.txt>");
    return 2;
}

var baseline = ProtoParser.Parse(await File.ReadAllTextAsync(args[0]));
var head = ProtoParser.Parse(await File.ReadAllTextAsync(args[1]));
var roots = (await File.ReadAllLinesAsync(args[2])).Select(line => line.Trim()).Where(line => line.Length > 0);
var violations = ParityChecker.Check(baseline, head, roots);
foreach (var violation in violations)
    Console.WriteLine(violation);
return violations.Count == 0 ? 0 : 1;
