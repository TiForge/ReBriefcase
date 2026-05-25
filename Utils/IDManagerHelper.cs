using System;
using System.Security.Cryptography;
using System.Text;

namespace BriefcaseTool.Utils;

public class IDManager
{
    static readonly char[] hexChars = "1234567890abcdef".ToCharArray();
    public static string GenerateHexID(int length)
    {
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);

        var ID = new StringBuilder(length);

        for (int written = 0; written < length; written++)
        {
            ID.Append(hexChars[bytes[written] & 0xF]);
        }
        
        return ID.ToString();
    }
}