using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using Tmds.DBus.Protocol;
using Xalia.Sdl;

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
            string iface, string member, int arg1, MessageValueReader<T> reply_reader)
        {
            return CallMethod(connection, peer, path, iface, member, "i", MessageFlags.None, (ref MessageWriter writer) =>
            {
                writer.WriteInt32(arg1);
            }, reply_reader);
        }

        public static Task<T> CallMethod<T>(Connection connection, string peer, string path,
            string iface, string member, uint arg1, MessageValueReader<T> reply_reader)
        {
            return CallMethod(connection, peer, path, iface, member, "u", MessageFlags.None, (ref MessageWriter writer) =>
            {
                writer.WriteUInt32(arg1);
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
            string iface, string member, string arg1)
        {
            return CallMethod(connection, peer, path, iface, member, "s", MessageFlags.None, (ref MessageWriter writer) =>
            {
                writer.WriteString(arg1);
            });
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

        public static Task<object> GetProperty(Connection connection, string peer, string path,
            string iface, string prop)
        {
            return CallMethod(connection, peer, path, IFACE_PROPERTIES,
                "Get", "ss", (ref MessageWriter writer) =>
                {
                    writer.WriteString(iface);
                    writer.WriteString(prop);
                }, ReadMessageVariant);
        } 

        public static Task SetProperty(Connection connection, string peer, string path,
            string iface, string prop, bool value)
        {
            return CallMethod(connection, peer, path, IFACE_PROPERTIES,
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

        public static int ReadMessageInt32(Message message, object state)
        {
            return message.GetBodyReader().ReadInt32();
        }

        public static uint ReadMessageUint32(Message message, object state)
        {
            return message.GetBodyReader().ReadUInt32();
        }

        public static object ReadMessageVariant(Message message, object state)
        {
            return message.GetBodyReader().ReadVariant();
        }

        public static (string, string) ReadMessageElement(Message message, object state)
        {
            var reader = message.GetBodyReader();
            reader.AlignStruct();
            var peer = reader.ReadString();
            var path = reader.ReadString();
            return (peer, path);
        }

        public static List<(string, string)> ReadMessageElementList(Message message, object state)
        {
            var result = new List<(string, string)>();
            var reader = message.GetBodyReader();
            var end = reader.ReadArrayStart(DBusType.Struct);
            while (reader.HasNext(end))
            {
                reader.AlignStruct();
                var peer = reader.ReadString();
                var path = reader.ReadString();
                result.Add((peer, path));
            }
            return result;
        }

        public struct AtSpiSignal
        {
            public string peer;
            public string path;
            public string detail;
            public int detail1;
            public int detail2;
            public object value;
            public Dictionary<string, object> properties;
        }

        private static AtSpiSignal ReadAtSpiSignal(Message message, object state)
        {
            var result = new AtSpiSignal();
            result.peer = message.SenderAsString;
            result.path = message.PathAsString;
            var reader = message.GetBodyReader();
            result.detail = reader.ReadString();
            result.detail1 = reader.ReadInt32();
            result.detail2 = reader.ReadInt32();
            result.value = reader.ReadVariant();
            var arraystart = reader.ReadArrayStart(DBusType.Struct);
            if (reader.HasNext(arraystart))
            {
                result.properties = new Dictionary<string, object>();
                do
                {
                    reader.AlignStruct();
                    var name = reader.ReadString();
                    var value = reader.ReadVariant();
                    result.properties[name] = value;
                } while (reader.HasNext(arraystart));
            }
            return result;
        }

        static bool debug_events = Utils.TryGetEnvironmentVariable("XALIA_DEBUG_EVENTS", out var debug) && debug != "0";

        public static Task<IDisposable> MatchAtSpiSignal(Connection connection, string iface, string name, Action<AtSpiSignal> handler)
        {
            bool debug_this_event = (debug_events ||
                (Utils.TryGetEnvironmentVariable($"XALIA_DEBUG_EVENT_{name.ToUpperInvariant()}", out var value) && value != "0"));
            var rule = new MatchRule();
            rule.Interface = iface;
            rule.Member = name;
            rule.Type = MessageType.Signal;
            return connection.AddMatchAsync(rule, ReadAtSpiSignal,
                (Exception e, AtSpiSignal signal, object st1, object st2) =>
                {
                    if (!(e is null))
                    {
                        Utils.OnError(e);
                        return;
                    }
                    if (debug_this_event)
                    {
                        Console.WriteLine($"AT-SPI2 EVENT: {iface}.{name} on {signal.peer}:{signal.path}");
                        if (!string.IsNullOrEmpty(signal.detail))
                            Console.WriteLine($"  detail: {signal.detail}");
                        if (signal.detail1 != 0)
                            Console.WriteLine($"  detail1: {signal.detail1}");
                        if (signal.detail2 != 0)
                            Console.WriteLine($"  detail2: {signal.detail2}");
                        if (!(signal.value is null))
                            Console.WriteLine($"  value: {signal.value}");
                        if (!(signal.properties is null))
                        {
                            foreach (var property in signal.properties)
                            {
                                Console.WriteLine($"  properties[\"{property.Key}\": {property.Value}");
                            }
                        }
                    }
                    try
                    {
                        // Use Send() to work around https://github.com/tmds/Tmds.DBus/issues/191
                        SdlSynchronizationContext.Instance.Send((object state) =>
                        {
                            var sig = (AtSpiSignal)state;
                            handler(sig);
                        }, signal);
                    }
                    catch (Exception e2)
                    {
                        Utils.OnError(e2);
                    }
                }).AsTask();
        }

        public const string SERVICE_DBUS = "org.freedesktop.DBus";
        public const string PATH_DBUS = "/org/freedesktop/DBus";
        public const string IFACE_DBUS = "org.freedesktop.DBus";
        public const string IFACE_PROPERTIES = "org.freedesktop.DBus.Properties";

        public const string SERVICE_BUS = "org.a11y.Bus";
        public const string PATH_BUS = "/org/a11y/bus";
        public const string IFACE_BUS = "org.a11y.Bus";
        public const string IFACE_STATUS = "org.a11y.Status";

        public const string SERVICE_REGISTRY = "org.a11y.atspi.Registry";
        public const string PATH_ACCESSIBLE_ROOT = "/org/a11y/atspi/accessible/root";
        public const string PATH_REGISTRY = "/org/a11y/atspi/registry";
        public const string IFACE_ACCESSIBLE = "org.a11y.atspi.Accessible";
        public const string IFACE_REGISTRY = "org.a11y.atspi.Registry";
        public const string IFACE_EVENT_OBJECT = "org.a11y.atspi.Event.Object";
    }
}
