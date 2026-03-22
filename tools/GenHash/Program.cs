using System.Security.Cryptography;
using System.Text;
var salt = RandomNumberGenerator.GetBytes(32);
var tokenBytes = Encoding.UTF8.GetBytes("test-token-agent");
var combined = new byte[salt.Length + tokenBytes.Length];
salt.CopyTo(combined, 0);
tokenBytes.CopyTo(combined, salt.Length);
var hash = SHA256.HashData(combined);
Console.WriteLine($"TradeAgent|{Convert.ToBase64String(salt)}|{Convert.ToBase64String(hash)}");
