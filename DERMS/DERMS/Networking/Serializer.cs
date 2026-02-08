using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace DERMS.Networking
{
    public static class Serializer
    {
        public static byte[] SerializeObject(object obj)
        {

            if (obj == null) return new byte[0];

            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
            
        }

        public static T DeserializeObject<T>(byte[] data) where T : new()
        {

            if (data == null || data.Length == 0) return new T();

            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream(data))
            {
                object o = bf.Deserialize(ms);
                if (o is T) return (T)o;
            }

            return new T();
        }

        public static byte[] ToUtf8(string s)
        {

            if (s == null) return new byte[0];
            return Encoding.UTF8.GetBytes(s);
        }

        public static string FromUtf8(byte[] data)
        {

            if (data == null || data.Length == 0) return "";
            return Encoding.UTF8.GetString(data);
        }
    }
}
