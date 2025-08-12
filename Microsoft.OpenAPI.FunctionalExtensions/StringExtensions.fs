module StringExtensions

type System.String with
    member s1.icompare(s2: string) = System.String.Equals(s1, s2, System.StringComparison.CurrentCultureIgnoreCase);