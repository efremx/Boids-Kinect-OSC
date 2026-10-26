using System;
using UnityEngine;

namespace ZigSimTools
{
    public class ZigSimDataManager : SingletonMonoBehaviour<ZigSimDataManager>
    {
        [SerializeField] private int port1 = 7777;
        [SerializeField] private int port2 = 7778;

        private UdpReceiver udpReceiver1;
        private UdpReceiver udpReceiver2;

        // Player 1 callbacks        
        public Action<Device, string> BasicDataCallBack_P1;
        public Action<Accel> AccelCallBack_P1;
        public Action<Gravity> GravityCallBack_P1;
        public Action<Gyro> GyroCallBack_P1;
        public Action<Quaternion> QuaternionCallBack_P1;
        public Action<Compass> CompassCallBack_P1;
        public Action<Pressure> PressureCallBack_P1;
        public Action<Gps> GpsCallBack_P1;
        public Action<MicLevel> MicLevelCallBack_P1;
        public Action<Touch[]> TouchCallBack_P1;

        // Player 2 callbacks
        public Action<Device, string> BasicDataCallBack_P2;
        public Action<Accel> AccelCallBack_P2;
        public Action<Gravity> GravityCallBack_P2;
        public Action<Gyro> GyroCallBack_P2;
        public Action<Quaternion> QuaternionCallBack_P2;
        public Action<Compass> CompassCallBack_P2;
        public Action<Pressure> PressureCallBack_P2;
        public Action<Gps> GpsCallBack_P2;
        public Action<MicLevel> MicLevelCallBack_P2;
        public Action<Touch[]> TouchCallBack_P2;


        protected override void Awake ()
        {
            base.Awake();
            var context = System.Threading.SynchronizationContext.Current;

            // -------- PLAYER 1 --------
            udpReceiver1 = new UdpReceiver(port1);
            udpReceiver1.MessageReceived += (s, e) =>
            {
                context.Post(_ =>
                {
                    var zigsimData = JsonUtility.FromJson<ZigSimData> (e.Message);
                    Debug.Log (zigsimData.sensordata.ToString ());

                    BasicDataCallBack_P1?.Invoke (zigsimData.device, zigsimData.timestamp);
                    AccelCallBack_P1?.Invoke (zigsimData.sensordata.accel);
                    GravityCallBack_P1?.Invoke (zigsimData.sensordata.gravity);
                    GyroCallBack_P1?.Invoke (zigsimData.sensordata.gyro);
                    QuaternionCallBack_P1?.Invoke (zigsimData.sensordata.quaternion);
                    CompassCallBack_P1?.Invoke (zigsimData.sensordata.compass);
                    PressureCallBack_P1?.Invoke (zigsimData.sensordata.pressure);
                    GpsCallBack_P1?.Invoke (zigsimData.sensordata.gps);
                    MicLevelCallBack_P1?.Invoke (zigsimData.sensordata.miclevel);
                    TouchCallBack_P1?.Invoke (zigsimData.sensordata.touch);

                }, null);
            };

            // -------- PLAYER 2 --------
            udpReceiver2 = new UdpReceiver(port2);
            udpReceiver2.MessageReceived += (s, e) =>
            {
                context.Post(_ =>
                {
                    var zigsimData = JsonUtility.FromJson<ZigSimData> (e.Message);
                    Debug.Log (zigsimData.sensordata.ToString ());

                    BasicDataCallBack_P2?.Invoke (zigsimData.device, zigsimData.timestamp);
                    AccelCallBack_P2?.Invoke (zigsimData.sensordata.accel);
                    GravityCallBack_P2?.Invoke (zigsimData.sensordata.gravity);
                    GyroCallBack_P2?.Invoke (zigsimData.sensordata.gyro);
                    QuaternionCallBack_P2?.Invoke (zigsimData.sensordata.quaternion);
                    CompassCallBack_P2?.Invoke (zigsimData.sensordata.compass);
                    PressureCallBack_P2?.Invoke (zigsimData.sensordata.pressure);
                    GpsCallBack_P2?.Invoke (zigsimData.sensordata.gps);
                    MicLevelCallBack_P2?.Invoke (zigsimData.sensordata.miclevel);
                    TouchCallBack_P2?.Invoke (zigsimData.sensordata.touch);

                }, null);
            };

            udpReceiver1.Disconnected += (s, e) => Debug.Log("P1 disconnected");
            udpReceiver2.Disconnected += (s, e) => Debug.Log("P2 disconnected");
        }

        public void StartReceiving()
        {
            Debug.Log("START RECEIVER P1: " + port1);
            Debug.Log("START RECEIVER P2: " + port2);

            udpReceiver1.StartReceiving();
            udpReceiver2.StartReceiving();
        }

        public async void StopReceiving()
        {
            await udpReceiver1.StopReceiving();
            await udpReceiver2.StopReceiving();
        }

        protected override async void OnDestroy()
        {
            await udpReceiver1.StopReceiving();
            await udpReceiver2.StopReceiving();
            base.OnDestroy();
        }

        private async void OnApplicationQuit()
        {
            await udpReceiver1.StopReceiving();
            await udpReceiver2.StopReceiving();
        }
    }
}



/*
using System;
using UnityEngine;

namespace ZigSimTools
{
    public class ZigSimDataManager : SingletonMonoBehaviour<ZigSimDataManager>
    {
        [SerializeField]
        private int portNumber = 7777;
        private UdpReceiver udpReceiver;

        public Action<Device, string> BasicDataCallBack;
        public Action<Accel> AccelCallBack;
        public Action<Gravity> GravityCallBack;
        public Action<Gyro> GyroCallBack;
        public Action<Quaternion> QuaternionCallBack;
        public Action<Compass> CompassCallBack;
        public Action<Pressure> PressureCallBack;
        public Action<Gps> GpsCallBack;
        public Action<MicLevel> MicLevelCallBack;
        public Action<Touch[]> TouchCallBack;

        protected override void Awake ()
        {
            base.Awake ();
            udpReceiver = new UdpReceiver (portNumber);
            var context = System.Threading.SynchronizationContext.Current;

            udpReceiver.MessageReceived += (s, e) =>
            {
                context.Post (_ =>
                {
                    var zigsimData = JsonUtility.FromJson<ZigSimData> (e.Message);
                    Debug.Log (zigsimData.sensordata.ToString ());

                    BasicDataCallBack?.Invoke (zigsimData.device, zigsimData.timestamp);
                    AccelCallBack?.Invoke (zigsimData.sensordata.accel);
                    GravityCallBack?.Invoke (zigsimData.sensordata.gravity);
                    GyroCallBack?.Invoke (zigsimData.sensordata.gyro);
                    QuaternionCallBack?.Invoke (zigsimData.sensordata.quaternion);
                    CompassCallBack?.Invoke (zigsimData.sensordata.compass);
                    PressureCallBack?.Invoke (zigsimData.sensordata.pressure);
                    GpsCallBack?.Invoke (zigsimData.sensordata.gps);
                    MicLevelCallBack?.Invoke (zigsimData.sensordata.miclevel);
                    TouchCallBack?.Invoke (zigsimData.sensordata.touch);
                }, null);
            };

            udpReceiver.Disconnected += (s, e) =>
            {
                Debug.Log ("udp client was closed.");
            };
        }

        public void StartReceiving ()
        {
            Debug.Log("START RECEIVER ON PORT: " + portNumber);
            udpReceiver.StartReceiving ();
            
        }

        public async void StopReceiving ()
        {
            await udpReceiver.StopReceiving ();
        }

        protected override async void OnDestroy ()
        {
            await udpReceiver.StopReceiving ();
            base.OnDestroy ();
        }

        private async void OnApplicationQuit ()
        {
            await udpReceiver.StopReceiving ();
        }
    }
}

*/