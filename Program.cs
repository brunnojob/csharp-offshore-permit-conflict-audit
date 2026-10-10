using System.Globalization;
using System.Text.Json;

static class Program
{
    static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    static double Numeric(string value)
    {
        double result = double.Parse(value, Culture);
        Require(double.IsFinite(result), "Finite numeric input required");
        return result;
    }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
    static List<string[]> Input(int columns)
    {
        var rows = new List<string[]>();
        string? line;
        while ((line = Console.ReadLine()) is not null)
        {
            Require(rows.Count < 100000, "Input capacity exceeded");
            string[] fields = line.Split(',');
            Require(fields.Length == columns && fields.All(f => !string.IsNullOrWhiteSpace(f)), "Invalid CSV row");
            rows.Add(fields);
        }
        Require(rows.Count > 0, "Missing input");
        return rows;
    }
    static int Main(string[] args)
    {
        try
        {
            if (args.SequenceEqual(new[] { "--self-test" })) { Test(); Console.WriteLine("Tests passed"); }
            else Run();
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 2; }
    }

    record Permit(string Id, string Asset, double Start, double End, string Mode, bool GasTest, bool Authorized);
    record Conflict(string Left, string Right, string Reason);
    static List<Conflict> Audit(List<Permit> permits)
    {
        var output = new List<Conflict>();
        Require(permits.Select(p => p.Id).Distinct().Count() == permits.Count, "Duplicate permit");
        foreach (var p in permits)
        {
            Require(p.Start >= 0 && p.End > p.Start && (p.Mode is "HOT" or "COLD" or "ENERGIZED"), "Invalid permit");
            if (!p.Authorized) output.Add(new Conflict(p.Id, "-", "AUTHORIZATION_MISSING"));
            if (p.Mode == "HOT" && !p.GasTest) output.Add(new Conflict(p.Id, "-", "GAS_TEST_MISSING"));
        }
        foreach (var group in permits.GroupBy(p => p.Asset))
        {
            var ordered = group.OrderBy(p => p.Start).ThenBy(p => p.Id).ToList();
            for (int i = 0; i < ordered.Count; i++)
            for (int j = i + 1; j < ordered.Count && ordered[j].Start < ordered[i].End; j++)
            {
                var a = ordered[i]; var b = ordered[j];
                if (a.Mode == "ENERGIZED" || b.Mode == "ENERGIZED" || a.Mode == "HOT" || b.Mode == "HOT") output.Add(new Conflict(a.Id, b.Id, "BOUNDARY_CONFLICT"));
            }
        }
        return output;
    }
    static void Test()
    {
        var results = Audit(new List<Permit> { new("P1", "M1", 0, 2, "HOT", false, true), new("P2", "M1", 1, 3, "ENERGIZED", true, true), new("P3", "M1", 3, 4, "COLD", true, true) });
        Require(results.Count == 2 && results.Any(c => c.Reason == "BOUNDARY_CONFLICT"), "Conflict audit mismatch");
        Require(Audit(new List<Permit> { new("P1", "M1", 0, 1, "COLD", false, true), new("P2", "M1", 1, 2, "HOT", true, true) }).Count == 0, "Adjacent intervals conflict");
    }
    static void Run()
    {
        var permits = Input(7).Select(r => { Require((r[5] is "0" or "1") && (r[6] is "0" or "1"), "Invalid evidence flag"); return new Permit(r[0], r[1], Numeric(r[2]), Numeric(r[3]), r[4], r[5] == "1", r[6] == "1"); }).ToList();
        foreach (var conflict in Audit(permits)) Console.WriteLine(JsonSerializer.Serialize(conflict));
    }

}
