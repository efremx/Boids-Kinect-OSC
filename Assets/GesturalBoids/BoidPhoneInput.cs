using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using extOSC;
using extOSC.Core;

namespace GesturalBoids
{
    public class BoidPhoneInput : MonoBehaviour
    {
        public enum Protocol { ZigSimJson, OSC, Off }
        public Protocol protocol = Protocol.ZigSimJson;
        public bool autoDetectProtocol = true;
        public bool useGyroFallback = true;
        public bool gyroInRadians = true;
        public Vector3 gyroAxisSigns = Vector3.one;
        public Vector3 orientationOffsetEuler;
        public int port = 7777;
        [Tooltip("OSC address patterns; * accepts the device identifier.")]
        public string accelAddress = "/ZIGSIM/*/accel";
        public string quaternionAddress = "/ZIGSIM/*/quaternion";
        public string gyroAddress = "/ZIGSIM/*/gyro";
        public float snapThreshold = 2.4f;
        public float resetThreshold = 1.2f;
        public float snapCooldown = 0.8f;
        public float staleSeconds = 1f;
        public event Action Snapped;
        public float Acceleration { get; private set; }
        public Vector3 Gyro { get; private set; }
        public int PacketCount { get; private set; }
        public string LocalAddresses { get; private set; } = "No active IPv4 adapter found";
        public string LastSender => latestSender;
        public bool LoopbackPassed { get; private set; }
        public bool Listening => running;
        public string PacketPreview { get; private set; } = "--";
        public int QuaternionPackets { get; private set; }
        public string LastAddress { get; private set; } = "--";
        public string LastPacketFormat { get; private set; } = "waiting";
        public bool Connected => Time.unscaledTime - lastPacket < staleSeconds;
        public string Status { get; private set; } = "Waiting for phone";
        public UnityEngine.Quaternion Orientation { get; private set; } = UnityEngine.Quaternion.identity;
        bool QuaternionLive => Time.unscaledTime - lastOrientation < staleSeconds;
        public bool HasOrientation => Connected && (QuaternionLive || (useGyroFallback && Time.unscaledTime - lastGyro < staleSeconds));
        public string RotationSource => QuaternionLive ? "quaternion live" : HasOrientation ? "gyro live (may drift)" : "no rotation data";
        readonly ConcurrentQueue<byte[]> messages = new ConcurrentQueue<byte[]>();
        UdpClient socket;
        Thread worker;
        volatile bool running;
        volatile string latestSender = "--";
        volatile string receiveError;
        bool armed = true;
        float lastPacket = -100, lastOrientation = -100, nextSnap;
        float lastGyro = -100;
        UnityEngine.Quaternion gyroOrientation = UnityEngine.Quaternion.identity;
        UnityEngine.Quaternion calibration = UnityEngine.Quaternion.identity;
        UnityEngine.Quaternion rawOrientation = UnityEngine.Quaternion.identity;
        UnityEngine.Quaternion neutralBasis = UnityEngine.Quaternion.identity;

        void OnEnable()
        {
            if (protocol == Protocol.Off) return;
            RefreshAddresses();
            try
            {
                    socket = new UdpClient(AddressFamily.InterNetwork);
                    socket.Client.Bind(new IPEndPoint(IPAddress.Any, port));
                    socket.Client.ReceiveTimeout = 1000;
                    running = true;
                    worker = new Thread(ReceiveLoop) { IsBackground = true };
                    worker.Start();
                Status = "Listening for " + (autoDetectProtocol ? "JSON / OSC" : protocol.ToString()) + " on UDP " + port;
                Invoke(nameof(CheckReceiver), 0.25f);
            }
            catch (Exception e) { Status = "Phone input: " + e.Message; Debug.LogWarning(Status, this); }
        }
        public void RefreshAddresses()
        {
            var addresses = new List<string>();
            try
            {
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback || adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    foreach (var entry in adapter.GetIPProperties().UnicastAddresses)
                        if (entry.Address.AddressFamily == AddressFamily.InterNetwork && !entry.Address.ToString().StartsWith("169.254.")) addresses.Add(adapter.Name + ": " + entry.Address);
                }
            }
            catch (NetworkInformationException) { }
            LocalAddresses = addresses.Count > 0 ? string.Join("\n", addresses) : "No active IPv4 adapter found";
        }
        public void CheckReceiver()
        {
            LoopbackPassed = false;
            if (!running) return;
            using (var sender = new UdpClient(AddressFamily.InterNetwork))
            {
                var bytes = Encoding.UTF8.GetBytes("{\"boids_receiver_test\":true}");
                sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, port));
            }
        }
        public void Reconnect(int newPort)
        {
            if (newPort < 1 || newPort > 65535) return;
            OnDisable(); port = newPort; receiveError = null; OnEnable();
        }

        static float Number(OSCValue value)
        {
            if (value.Type == OSCValueType.Double) return (float)value.DoubleValue;
            if (value.Type == OSCValueType.Int) return value.IntValue;
            return value.Type == OSCValueType.Float ? value.FloatValue : 0;
        }

        void ReceiveLoop()
        {
            var client = socket;
            while (running)
            {
                try
                {
                    IPEndPoint sender = null;
                    var packet = client.Receive(ref sender);
                    latestSender = sender.ToString();
                    if (messages.Count < 256) messages.Enqueue(packet);
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.TimedOut || e.SocketErrorCode == SocketError.WouldBlock || e.SocketErrorCode == SocketError.ConnectionReset) continue;
                    if (running) receiveError = e.Message;
                    break;
                }
                catch (ObjectDisposedException) { break; }
            }
            running = false;
        }

        void Update()
        {
            if (receiveError != null) Status = "Receiver stopped: " + receiveError;
            int budget = 256;
            while (budget-- > 0 && messages.TryDequeue(out var packet))
            {
                try
                {
                    int first = 0;
                    while (first < packet.Length && packet[first] <= 32) first++;
                    bool json = first < packet.Length && packet[first] == '{';
                    string text = json ? Encoding.UTF8.GetString(packet) : null;
                    if (json && text.Contains("\"boids_receiver_test\"")) { LoopbackPassed = true; continue; }
                    PacketCount++; lastPacket = Time.unscaledTime;
                    PacketPreview = json ? text.Substring(0, Mathf.Min(140, text.Length)) : BitConverter.ToString(packet, 0, Mathf.Min(32, packet.Length));
                    LastPacketFormat = json ? "JSON" : "OSC";
                    if (!autoDetectProtocol && json != (protocol == Protocol.ZigSimJson)) { Status = "Protocol mismatch: " + LastPacketFormat; continue; }
                    if (!json)
                    {
                        if (packet.Length >= 8 && (packet[0] == '/' || packet[0] == '#')) ProcessOSC(OSCConverter.Unpack(packet), 0);
                        else Status = "Unknown UDP packet / check phone format";
                        continue;
                    }
                    LastAddress = "sensordata";
                    var data = JsonUtility.FromJson<ZigSimTools.ZigSimData>(text)?.sensordata;
                    if (data == null) continue;
                    if (data.accel != null) ReceiveAccel(new Vector3((float)data.accel.x, (float)data.accel.y, (float)data.accel.z));
                    if (data.quaternion != null) ReceiveOrientation(new UnityEngine.Quaternion((float)data.quaternion.x, (float)data.quaternion.y, (float)data.quaternion.z, (float)data.quaternion.w));
                    if (data.gyro != null) ReceiveGyro(new Vector3((float)data.gyro.x, (float)data.gyro.y, (float)data.gyro.z));
                    Status = "Phone receiving JSON";
                }
                catch (Exception e) when (e is ArgumentException || e is IndexOutOfRangeException || e is NullReferenceException || e is System.Collections.Generic.KeyNotFoundException || e is InvalidCastException)
                { Status = "Unreadable " + LastPacketFormat + " packet / check phone format"; }
            }
            if (!QuaternionLive && useGyroFallback && Time.unscaledTime - lastGyro < staleSeconds)
            {
                var degrees = Vector3.Scale(Gyro, gyroAxisSigns) * (gyroInRadians ? Mathf.Rad2Deg : 1);
                gyroOrientation *= UnityEngine.Quaternion.Euler(degrees * Mathf.Min(Time.unscaledDeltaTime, 0.05f));
                Orientation = UnityEngine.Quaternion.Euler(orientationOffsetEuler) * neutralBasis * gyroOrientation;
            }
        }

        void ReceiveGyro(Vector3 value)
        {
            if (!Finite(value.sqrMagnitude)) return;
            Gyro = value; lastGyro = Time.unscaledTime;
        }
        void ProcessOSC(IOSCPacket packet, int depth)
        {
            if (depth > 16) return;
            if (packet is OSCBundle bundle) { foreach (var child in bundle.Packets) ProcessOSC(child, depth + 1); return; }
            if (!(packet is OSCMessage message)) return;
            LastAddress = message.Address;
            string address = message.Address.TrimEnd('/').ToLowerInvariant();
            bool accel = Match(address, accelAddress, "accel"), gyro = Match(address, gyroAddress, "gyro"), quat = Match(address, quaternionAddress, "quaternion");
            var values = message.Values;
            if (quat && values.Count >= 4) ReceiveOrientation(new UnityEngine.Quaternion(Number(values[0]), Number(values[1]), Number(values[2]), Number(values[3])));
            else if (values.Count >= 3 && (accel || gyro))
            {
                var vector = new Vector3(Number(values[0]), Number(values[1]), Number(values[2]));
                if (accel) ReceiveAccel(vector); else ReceiveGyro(vector);
            }
            Status = accel || gyro || quat ? "Phone receiving OSC" : "OSC received / sensor address unrecognized";
        }
        bool Match(string address, string pattern, string sensor)
        {
            if (autoDetectProtocol && address.EndsWith("/" + sensor, StringComparison.OrdinalIgnoreCase)) return true;
            return System.Text.RegularExpressions.Regex.IsMatch(address, "^" + System.Text.RegularExpressions.Regex.Escape(pattern.ToLowerInvariant()).Replace("\\*", "[^/]*") + "$");
        }

        void ReceiveAccel(Vector3 value)
        {
            if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z)) return;
            lastPacket = Time.unscaledTime;
            Acceleration = value.magnitude;
            Status = "Phone receiving";
            if (Acceleration < resetThreshold) armed = true;
            if (armed && Acceleration >= snapThreshold && Time.unscaledTime >= nextSnap)
            {
                armed = false;
                nextSnap = Time.unscaledTime + snapCooldown;
                Snapped?.Invoke();
            }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        void ReceiveOrientation(UnityEngine.Quaternion value)
        {
            float length = value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
            if (!Finite(length) || length < 0.01f) return;
            rawOrientation = value.normalized;
            Orientation = UnityEngine.Quaternion.Euler(orientationOffsetEuler) * neutralBasis * calibration * rawOrientation;
            gyroOrientation = calibration * rawOrientation;
            QuaternionPackets++;
            lastOrientation = lastPacket = Time.unscaledTime;
        }
        public void Calibrate(bool screenUpOnTable = true)
        {
            if (!HasOrientation) return;
            calibration = QuaternionLive ? UnityEngine.Quaternion.Inverse(rawOrientation) : UnityEngine.Quaternion.Inverse(gyroOrientation) * calibration;
            // Flat phone: blade points away along the stage; lifting the phone raises the blade.
            neutralBasis = screenUpOnTable ? UnityEngine.Quaternion.Euler(-90,0,0) : UnityEngine.Quaternion.identity;
            gyroOrientation = UnityEngine.Quaternion.identity;
            Orientation = UnityEngine.Quaternion.Euler(orientationOffsetEuler) * neutralBasis;
        }
        void OnDisable()
        {
            CancelInvoke(nameof(CheckReceiver));
            running = false;
            socket?.Close(); // Close first to unblock Receive before waiting for the thread.
            worker?.Join(300);
            socket = null;
            while (messages.TryDequeue(out _)) { }
            lastPacket = lastOrientation = lastGyro = -100;
            Gyro = Vector3.zero;
            armed = true;
        }
    }
}
