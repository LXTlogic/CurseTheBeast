﻿using System.Text;

namespace CurseTheBeast.Utils;


public static class PathUtils
{
    public static string EscapeFileName(string str) => 
        Path.GetInvalidFileNameChars().Aggregate(new StringBuilder(str), (sb, c) => sb.Replace(c, '_')).ToString();
}
