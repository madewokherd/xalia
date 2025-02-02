﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

        public static Task<VariantValue> GetProperty(Connection connection, string peer, string path,
            string iface, string prop)
        {
            return CallMethod(connection, peer, path, IFACE_PROPERTIES,
                "Get", "ss", (ref MessageWriter writer) =>
                {
                    writer.WriteString(iface);
                    writer.WriteString(prop);
                }, ReadMessageVariant);
        } 

        public static async Task<double> GetPropertyDouble(Connection connection, string peer, string path,
            string iface, string prop)
        {
            return (await GetProperty(connection, peer, path, iface, prop)).GetDouble();
        }

        public static async Task<int> GetPropertyInt(Connection connection, string peer, string path,
            string iface, string prop)
        {
            return (await GetProperty(connection, peer, path, iface, prop)).GetInt32();
        }

        public static async Task<string> GetPropertyString(Connection connection, string peer, string path,
            string iface, string prop)
        {
            return (await GetProperty(connection, peer, path, iface, prop)).GetString();
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

        public static Task SetProperty(Connection connection, string peer, string path,
            string iface, string prop, double value)
        {
            return CallMethod(connection, peer, path, IFACE_PROPERTIES,
                "Set", "ssv", (ref MessageWriter writer) =>
                {
                    writer.WriteString(iface);
                    writer.WriteString(prop);
                    writer.WriteVariantDouble(value);
                });
        }

        public static string ReadMessageString(Message message, object state)
        {
            return message.GetBodyReader().ReadString();
        }

        public static string[] ReadMessageStringArray(Message message, object state)
        {
            var reader = message.GetBodyReader();
            var result = new List<string>();
            var array = reader.ReadArrayStart(DBusType.String);
            while (reader.HasNext(array))
            {
                result.Add(reader.ReadString());
            }
            return result.ToArray();
        }

        public static Dictionary<string,string> ReadMessageStringDictionary(Message message, object state)
        {
            var reader = message.GetBodyReader();
            var result = new Dictionary<string, string>();
            var array = reader.ReadArrayStart(DBusType.DictEntry);
            while (reader.HasNext(array))
            {
                var key = reader.ReadString();
                var value = reader.ReadString();
                result[key] = value;
            }
            return result;
        }

        public static int ReadMessageInt32(Message message, object state)
        {
            return message.GetBodyReader().ReadInt32();
        }

        public static uint ReadMessageUint32(Message message, object state)
        {
            return message.GetBodyReader().ReadUInt32();
        }

        public static uint[] ReadMessageUint32Array(Message message, object state)
        {
            List<uint> result = new List<uint>();
            var reader = message.GetBodyReader();
            var array = reader.ReadArrayStart(DBusType.UInt32);
            while (reader.HasNext(array))
            {
                result.Add(reader.ReadUInt32());
            }
            return result.ToArray();
        }

        public static VariantValue ReadMessageVariant(Message message, object state)
        {
            return message.GetBodyReader().ReadVariantValue();
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

        public static (int, int, int, int) ReadMessageExtents(Message message, object state)
        {
            var reader = message.GetBodyReader();
            reader.AlignStruct();
            return (reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        }

        public static bool ReadMessageBoolean(Message message, object state)
        {
            var reader = message.GetBodyReader();
            return reader.ReadBool();
        }

        public struct AtSpiSignal
        {
            public string peer;
            public string path;
            public string detail;
            public int detail1;
            public int detail2;
            public VariantValue value;
            public Dictionary<string, VariantValue> properties;
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
            result.value = reader.ReadVariantValue();
            if (message.SignatureAsString == "siiv(so)")
            {
                // Qt does not include the array at the end and instead repeats the sender/path
                return result;
            }
            var arraystart = reader.ReadArrayStart(DBusType.Struct);
            if (reader.HasNext(arraystart))
            {
                result.properties = new Dictionary<string, VariantValue>();
                do
                {
                    reader.AlignStruct();
                    var name = reader.ReadString();
                    var value = reader.ReadVariantValue();
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
                        Utils.DebugWriteLine($"AT-SPI2 EVENT: {iface}.{name} on {signal.peer}:{signal.path}");
                        if (!string.IsNullOrEmpty(signal.detail))
                            Utils.DebugWriteLine($"  detail: {signal.detail}");
                        if (signal.detail1 != 0)
                            Utils.DebugWriteLine($"  detail1: {signal.detail1}");
                        if (signal.detail2 != 0)
                            Utils.DebugWriteLine($"  detail2: {signal.detail2}");
                        if (signal.value.Type != VariantValueType.Invalid)
                            Utils.DebugWriteLine($"  value: {signal.value}");
                        if (!(signal.properties is null))
                        {
                            foreach (var property in signal.properties)
                            {
                                Utils.DebugWriteLine($"  properties[\"{property.Key}\"]: {property.Value}");
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
                }, ObserverFlags.None).AsTask();
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
        public const string IFACE_ACTION = "org.a11y.atspi.Action";
        public const string IFACE_APPLICATION = "org.a11y.atspi.Application";
        public const string IFACE_COMPONENT = "org.a11y.atspi.Component";
        public const string IFACE_REGISTRY = "org.a11y.atspi.Registry";
        public const string IFACE_EVENT_OBJECT = "org.a11y.atspi.Event.Object";
        public const string IFACE_EVENT_WINDOW = "org.a11y.atspi.Event.Window";
        public const string IFACE_SELECTION = "org.a11y.atspi.Selection";
        public const string IFACE_VALUE = "org.a11y.atspi.Value";

        public const uint ATSPI_COORD_TYPE_SCREEN = 0;
        public const uint ATSPI_COORD_TYPE_WINDOW = 1;
        public const uint ATSPI_COORD_TYPE_PARENT = 2;
    }
}
