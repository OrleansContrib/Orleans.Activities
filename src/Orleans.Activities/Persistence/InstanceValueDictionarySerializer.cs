using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.IO.Compression;
using System.Runtime.DurableInstancing;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Linq;
using Orleans.CodeGeneration;
using Orleans.Serialization;

namespace Orleans.Activities.Persistence
{
    // TODO
    // ActivityExecutor works only with NetDataContractSerializer and Orleans fails on InstanceValue, that is not [Serializable].
    // Isn't is possible, to use Orleans serializers in some way?

    [Serializer(typeof(IDictionary<XName, InstanceValue>))]
    public static class InstanceValueDictionarySerializer
    {
        // TODO It supposes, that workflow state won't change during persistence. Is it true?
        [CopierMethod]
        public static object DeepCopy(object item, ICopyContext context) => item;

        [SerializerMethod]
        public static void Serializer(object item, ISerializationContext context, Type expected)
        {
            var buffer = Serialize(item);
            context.StreamWriter.Write(buffer.Length);
            context.StreamWriter.Write(buffer);
        }

        [DeserializerMethod]
        public static object Deserializer(Type expected, IDeserializationContext context)
            => Deserialize(context.StreamReader.ReadBytes(context.StreamReader.ReadInt()));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public static byte[] Serialize(object graph)
        {
            using (var memStream = new MemoryStream())
            using (var zipStream = new GZipStream(memStream, CompressionMode.Compress))
            {
                using (var xmlDictionaryWriter = XmlDictionaryWriter.CreateBinaryWriter(zipStream))
                {
                    var serializer = new NetDataContractSerializer();
                    serializer.WriteObject(xmlDictionaryWriter, graph);
                    xmlDictionaryWriter.Close();
                }
                zipStream.Close();
                return memStream.ToArray();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public static object Deserialize(byte[] buffer)
        {
            using (var memStream = new MemoryStream(buffer))
            using (var zipStream = new GZipStream(memStream, CompressionMode.Decompress))
            using (var xmlDictionaryReader = XmlDictionaryReader.CreateBinaryReader(zipStream, XmlDictionaryReaderQuotas.Max))
            {
                var serializer = new NetDataContractSerializer();
                return serializer.ReadObject(xmlDictionaryReader);
            }
        }
    }
}
