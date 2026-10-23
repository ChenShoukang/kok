using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Security.Cryptography;

namespace kok {
    public class VaultInfo {public int version=1,iterations=210000;public string salt,proof;}
    public sealed class VaultSession:IDisposable {
        byte[] encryptionKey,authenticationKey;
        bool disposed;
        VaultSession(string password,byte[] salt,int iterations){using(var derive=new Rfc2898DeriveBytes(password,salt,iterations,HashAlgorithmName.SHA256)){var keys=derive.GetBytes(64);encryptionKey=keys.Take(32).ToArray();authenticationKey=keys.Skip(32).ToArray();Array.Clear(keys,0,keys.Length);}}
        static byte[] Random(int length){var result=new byte[length];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(result);return result;}
        public static VaultSession Create(string directory,string password){
            if(password==null||password.Length<8||password.Length>256)throw new InvalidDataException("私密密码需为 8 至 256 个字符。");
            Directory.CreateDirectory(directory);string file=Path.Combine(directory,"vault-info.json");if(File.Exists(file))throw new InvalidOperationException("私密文件夹已存在，请使用密码解锁。");
            var info=new VaultInfo{salt=Convert.ToBase64String(Random(32))};var session=new VaultSession(password,Convert.FromBase64String(info.salt),info.iterations);
            try{info.proof=Convert.ToBase64String(session.Seal(Encoding.UTF8.GetBytes("KOK-PRIVATE-VAULT-V1"),"vault-check"));File.WriteAllText(file+".tmp",Library.Json.Serialize(info),new UTF8Encoding(false));File.Move(file+".tmp",file);return session;}catch{session.Dispose();throw;}
        }
        public static VaultSession Open(string directory,string password){
            if(password==null||password.Length>256)throw new InvalidDataException("私密密码不正确，或资料库已损坏。");VaultSession session=null;
            try{var info=Library.Json.Deserialize<VaultInfo>(File.ReadAllText(Path.Combine(directory,"vault-info.json"),Encoding.UTF8));var salt=Convert.FromBase64String(info.salt);if(info.version!=1||salt.Length!=32||info.iterations<100000||info.iterations>1000000)throw new FormatException();session=new VaultSession(password,salt,info.iterations);if(Encoding.UTF8.GetString(session.Unseal(Convert.FromBase64String(info.proof),"vault-check"))!="KOK-PRIVATE-VAULT-V1")throw new CryptographicException();return session;}
            catch{if(session!=null)session.Dispose();throw new InvalidDataException("私密密码不正确，或资料库已损坏。");}
        }
        void Check(){if(disposed)throw new InvalidOperationException("私密文件夹已锁定。");}
        byte[] Tag(byte[] bytes,string purpose){using(var hmac=new HMACSHA256(authenticationKey)){var prefix=Encoding.UTF8.GetBytes(purpose+"\0");var signed=new byte[prefix.Length+bytes.Length];Buffer.BlockCopy(prefix,0,signed,0,prefix.Length);Buffer.BlockCopy(bytes,0,signed,prefix.Length,bytes.Length);return hmac.ComputeHash(signed);}}
        public byte[] Seal(byte[] plain,string purpose){Check();using(var aes=Aes.Create()){aes.Key=encryptionKey;aes.IV=Random(16);aes.Mode=CipherMode.CBC;aes.Padding=PaddingMode.PKCS7;byte[] encrypted;using(var transform=aes.CreateEncryptor())encrypted=transform.TransformFinalBlock(plain,0,plain.Length);var payload=new byte[5+16+encrypted.Length];Buffer.BlockCopy(Encoding.ASCII.GetBytes("KOKV1"),0,payload,0,5);Buffer.BlockCopy(aes.IV,0,payload,5,16);Buffer.BlockCopy(encrypted,0,payload,21,encrypted.Length);var tag=Tag(payload,purpose);return payload.Concat(tag).ToArray();}}
        public byte[] Unseal(byte[] bytes,string purpose){Check();if(bytes==null||bytes.Length<69||Encoding.ASCII.GetString(bytes,0,5)!="KOKV1")throw new InvalidDataException("私密文件夹校验失败，原数据已保留。");var payload=bytes.Take(bytes.Length-32).ToArray();var expected=Tag(payload,purpose);int mismatch=0;for(int n=0;n<32;n++)mismatch|=expected[n]^bytes[bytes.Length-32+n];if(mismatch!=0)throw new InvalidDataException("私密文件夹校验失败，原数据已保留。");using(var aes=Aes.Create()){aes.Key=encryptionKey;aes.IV=payload.Skip(5).Take(16).ToArray();aes.Mode=CipherMode.CBC;aes.Padding=PaddingMode.PKCS7;using(var transform=aes.CreateDecryptor())return transform.TransformFinalBlock(payload,21,payload.Length-21);}}
        public void Dispose(){if(disposed)return;disposed=true;Array.Clear(encryptionKey,0,encryptionKey.Length);Array.Clear(authenticationKey,0,authenticationKey.Length);}
    }
}
