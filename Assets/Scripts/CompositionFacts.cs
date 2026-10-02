using System.Collections.Generic;
using System.Globalization;

public static class CompositionFacts
{
    static string Number(double value, string format) { return value.ToString(format, CultureInfo.InvariantCulture); }
    public static double MolecularMass(Lab.Mol molecule)
    {
        double mass = 0;
        foreach (var atom in molecule.Atoms) if (atom != null && atom.El != null) mass += atom.El.Mass;
        return mass;
    }
    public static int CountFormula(IEnumerable<Lab.Mol> molecules, string formula)
    {
        int count = 0;
        foreach (var molecule in molecules)
            if (molecule.Atoms.Count > 1 && molecule.Formula == formula) count++;
        return count;
    }
    public static string ZoneSummary(IEnumerable<Lab.Mol> molecules)
    {
        var counts = new SortedDictionary<string, int>();
        foreach (var molecule in molecules)
        {
            if (molecule.Atoms.Count < 2) continue;
            int old; counts.TryGetValue(molecule.Formula, out old);
            counts[molecule.Formula] = old + 1;
        }
        var parts = new List<string>();
        foreach (var pair in counts) parts.Add((pair.Value > 1 ? pair.Value.ToString() : "") + pair.Key);
        return string.Join(" + ", parts.ToArray());
    }
    public static string Describe(Lab.Mol molecule)
    {
        var counts = new SortedDictionary<string, int>();
        var masses = new Dictionary<string, double>();
        int atoms = 0;
        foreach (var atom in molecule.Atoms)
        {
            if (atom == null || atom.El == null) continue;
            string symbol = atom.El.Sym; int old; double oldMass;
            counts.TryGetValue(symbol, out old); masses.TryGetValue(symbol, out oldMass);
            counts[symbol] = old + 1; masses[symbol] = oldMass + atom.El.Mass; atoms++;
        }
        double total = MolecularMass(molecule);
        var atomParts = new List<string>(); var massParts = new List<string>();
        foreach (var pair in counts)
        {
            atomParts.Add(pair.Key + " " + Number(atoms > 0 ? 100.0 * pair.Value / atoms : 0, "0.0") + "%");
            massParts.Add(pair.Key + " " + Number(total > 0 ? 100.0 * masses[pair.Key] / total : 0, "0.0") + "%");
        }
        return Lang.T("Молекулярная масса: ", "Molecular mass: ") + Number(total, "0.###") +
            Lang.T(" а.е.м. (молярная: ", " u (molar: ") + Number(total, "0.###") + Lang.T(" г/моль)", " g/mol)") +
            "\n" + Lang.T("По числу атомов: ", "By atom count: ") + string.Join(" · ", atomParts.ToArray()) +
            "\n" + Lang.T("По массе: ", "By mass: ") + string.Join(" · ", massParts.ToArray());
    }
}
