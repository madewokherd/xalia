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

        public static Task<T> CallMethod<T>(Connection connection, string peer, string path,
            string iface, string member, string signature, MessageFlags flags, ObjectWriter body_writer,
            MessageValueReader<T> reply_reader)
        {
            MessageWriter message = connection.GetMessageWriter();
            try
            {

                message.WriteMethodCallHeader(peer, path, iface, member, signature, flags);

                body_writer(ref message);

                var buffer = message.CreateMessage();

                return connection.CallMethodAsync<T>(buffer, reply_reader);
            }
            finally
            {
                message.Dispose();
            }
        } 

        public static Task CallMethod(Connection connection, string peer, string path,
            string iface, string member, string signature, MessageFlags flags, ObjectWriter body_writer)
        {
            MessageWriter message = connection.GetMessageWriter();
            try
            {

                message.WriteMethodCallHeader(peer, path, iface, member, signature, flags);

                body_writer(ref message);

                var buffer = message.CreateMessage();

                return connection.CallMethodAsync(buffer);
            }
            finally
            {
                message.Dispose();
            }
        } 

        public static Task CallMethod(Connection connection, string peer, string path,
            string iface, string member, string signature, ObjectWriter body_writer)
        {
            return CallMethod(connection, peer, path, iface, member, signature, MessageFlags.None, body_writer);
        } 

        public static Task<T> CallMethod<T>(Connection connection, string peer, string path,
            string iface, string member, string signature, ObjectWriter body_writer, MessageValueReader<T> reply_reader)
        {
            return CallMethod(connection, peer, path, iface, member, signature, MessageFlags.None, body_writer, reply_reader);
        }

        public static Task<T> CallMethod<T>(Connection connection, string peer, string path,
            string iface, string member, MessageValueReader<T> reply_reader)
        {
            return CallMethod(connection, peer, path, iface, member, null, MessageFlags.None, WriteNoObjects, reply_reader);
        }

        public static Task<T> CallMethod<T>(Connection connection, string peer, string path,
            string iface, string member, string arg1, MessageValueReader<T> reply_reader)
        {
            return CallMethod(connection, peer, path, iface, member, "s", MessageFlags.None, (ref MessageWriter writer) =>
            {
                writer.WriteString(arg1);
            }, reply_reader);
        }

        public static Task<T> CallMethod<T>(Connection connection, string peer, string path,
            string iface, string member, string arg1, uint arg2, MessageValueReader<T> reply_reader)
        {
            return CallMethod(connection, peer, path, iface, member, "su", MessageFlags.None, (ref MessageWriter writer) =>
            {
                writer.WriteString(arg1);
                writer.WriteUInt32(arg2);
            }, reply_reader);
        }

        public static Task<T> CallMethod<T>(Connection connection, string peer, string path,
            string iface, string member, string arg1, int arg2, MessageValueReader<T> reply_reader)
        {
            return CallMethod(connection, peer, path, iface, member, "si", MessageFlags.None, (ref MessageWriter writer) =>
            {
                writer.WriteString(arg1);
                writer.WriteInt32(arg2);
            }, reply_reader);
        }

        public static Task CallMethod(Connection connection, string peer, string path,
            string iface, string member, string arg1, uint arg2)
        {
            return CallMethod(connection, peer, path, iface, member, "su", MessageFlags.None, (ref MessageWriter writer) =>
            {
                writer.WriteString(arg1);
                writer.WriteUInt32(arg2);
            });
        }

        public static Task CallMethod(Connection connection, string peer, string path,
            string iface, string member, string arg1, int arg2)
        {
            return CallMethod(connection, peer, path, iface, member, "si", MessageFlags.None, (ref MessageWriter writer) =>
            {
                writer.WriteString(arg1);
                writer.WriteInt32(arg2);
            });
        }

        public static Task SetProperty(Connection connection, string peer, string path,
            string iface, string prop, bool value)
        {
            return CallMethod(connection, peer, path, "org.freedesktop.DBus.Properties",
                "Set", "ssv", (ref MessageWriter writer) =>
                {
                    writer.WriteString(iface);
                    writer.WriteString(prop);
                    writer.WriteVariantBool(value);
                });
        }

        public static string ReadMessageString(Message message, object state)
        {
            return message.GetBodyReader().ReadString();
        }

        public static uint ReadMessageUint32(Message message, object state)
        {
            return message.GetBodyReader().ReadUInt32();
        }
    }
}
