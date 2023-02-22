using System;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace Xalia.AtSpi2
{
    internal static class DBusUtils
    {
        public delegate void ObjectWriter(ref MessageWriter writer);

        public static void WriteNoObjects(ref MessageWriter writer) { }

        public static MessageBuffer CreateMethodCall(Connection connection, string peer, string path,
            string iface, string member, string signature, MessageFlags flags, ObjectWriter writer)
        {
            MessageWriter message = connection.GetMessageWriter();
            try
            {

                message.WriteMethodCallHeader(peer, path, iface, member, signature, flags);

                writer(ref message);

                return message.CreateMessage();
            }
            finally
            {
                message.Dispose();
            }
        } 

        public static MessageBuffer CreateMethodCall(Connection connection, string peer, string path,
            string iface, string member, string signature, ObjectWriter writer)
        {
            return CreateMethodCall(connection, peer, path, iface, member, signature, MessageFlags.None, writer);
        }

        public static MessageBuffer CreateMethodCall(Connection connection, string peer, string path,
            string iface, string member)
        {
            return CreateMethodCall(connection, peer, path, iface, member, null, MessageFlags.None, WriteNoObjects);
        }

        public static async Task SetProperty(Connection connection, string peer, string path,
            string iface, string prop, bool value)
        {
            var message = CreateMethodCall(connection, peer, path, "org.freedesktop.DBus.Properties",
                "Set", "ssv", (ref MessageWriter writer) =>
                {
                    writer.WriteString(iface);
                    writer.WriteString(prop);
                    writer.WriteVariantBool(value);
                });

            await connection.CallMethodAsync(message);
        }

        public static string ReadMessageString(Message message, object state)
        {
            return message.GetBodyReader().ReadString();
        }
    }
}
