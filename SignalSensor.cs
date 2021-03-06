/**
 * Copyright (c) 2019-2021 LG Electronics, Inc.
 *
 * This software contains code licensed as described in LICENSE.
 *
 */

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Simulator.Bridge;
using Simulator.Bridge.Data;
using Simulator.Sensors.UI;
using Simulator.Utilities;
using Simulator.Map;

namespace Simulator.Sensors
{
    [SensorType("Signal", new[] { typeof(SignalDataArray) })]
    public class SignalSensor : SensorBase
    {
        [SensorParameter]
        [Range(1f, 100f)]
        public float Frequency = 1.0f;

        [SensorParameter]
        [Range(1f, 1000f)]
        public float MaxDistance = 100.0f;

        private BridgeInstance Bridge;
        private Publisher<SignalDataArray> Publish;

        private uint SeqId;
        private float NextSend;

        private Dictionary<MapSignal, SignalData> DetectedSignals = new Dictionary<MapSignal, SignalData>();
        private MapSignal[] Visualized = Array.Empty<MapSignal>();
        private MapManager MapManager;
        private WireframeBoxes WireframeBoxes;

        [AnalysisMeasurement(MeasurementType.Misc)]
        public List<string> DetectedStates = new List<string>();

        [AnalysisMeasurement(MeasurementType.Count)]
        public int SignalsDetected => DetectedSignals.Count;

        public override SensorDistributionType DistributionType => SensorDistributionType.MainOrClient;
        public override float PerformanceLoad { get; } = 0.05f;

        protected override void Initialize()
        {
            WireframeBoxes = SimulatorManager.Instance.WireframeBoxes;
            MapManager = SimulatorManager.Instance.MapManager;
            NextSend = Time.time + 1.0f / Frequency;
        }

        protected override void Deinitialize()
        {
            
        }

        void Update()
        {
            if (Bridge != null && Bridge.Status == Status.Connected)
            {
                if (Time.time < NextSend)
                {
                    return;
                }
                NextSend = Time.time + 1.0f / Frequency;

                Publish(new SignalDataArray()
                {
                    Time = SimulatorManager.Instance.CurrentTime,
                    Sequence = SeqId++,
                    Data = DetectedSignals.Values.ToArray(),
                });
            }

            Visualized = DetectedSignals.Keys.ToArray();
        }

        private void FixedUpdate()
        {
            // Detected is updated OnTriggerStay.
            // So we clear it in FixedUpdate() which happens before OnTriggerStay.
            // Details of excution order can be found at: https://docs.unity3d.com/Manual/ExecutionOrder.html
            DetectedSignals.Clear();
        }

        void OnTriggerStay(Collider other)
        {
            var currentLane = other.GetComponentInParent<MapTrafficLane>();
            if (currentLane)
            {
                if (currentLane.stopLine?.isStopSign == false)
                {
                    GameObject egoGO = transform.parent.gameObject;
                    var signals = currentLane.stopLine.signals;
                    foreach (var signal in signals)
                    {
                        Vector3 relPos = egoGO.transform.InverseTransformPoint(signal.gameObject.transform.position);
                        relPos.Set(relPos.z, -relPos.x, relPos.y);

                        var forwardDistance = relPos.x;
                        if (forwardDistance > 0 && forwardDistance < MaxDistance)
                        {
                            Quaternion relRot = Quaternion.Inverse(egoGO.transform.rotation) * signal.gameObject.transform.rotation;
                            relRot.Set(-relRot.z, relRot.x, -relRot.y, relRot.w);

                            Vector3 size = signal.CurrentSignalLight.Bounds.size;
                            size.Set(size.z, size.x, size.y);

                            string id = signal.id;
                            if (string.IsNullOrEmpty(id))
                            {
                                id = "signal_" + signal.SeqId.ToString();
                            }

                            if (!DetectedStates.Contains(signal.CurrentState))
                            {
                                DetectedStates.Add(signal.CurrentState);
                            }

                            if (!DetectedSignals.ContainsKey(signal))
                            {
                                var signalData = new SignalData()
                                {
                                    SeqId = signal.SeqId,
                                    Id = id,
                                    Label = signal.CurrentState,
                                    Score = 1.0f,
                                    Position = relPos,
                                    Rotation = relRot,
                                    Scale = size,
                                };

                                DetectedSignals.Add(signal, signalData);
                            }
                        }
                    }
                }
            }
        }

        public override void OnBridgeSetup(BridgeInstance bridge)
        {
            Bridge = bridge;
            Publish = Bridge.AddPublisher<SignalDataArray>(Topic);
        }

        public override void OnVisualize(Visualizer visualizer)
        {
            foreach (var signal in Visualized)
            {
                Color color;
                switch (signal.CurrentState)
                {
                    case "green":
                        color = Color.green;
                        break;
                    case "yellow":
                        color = Color.yellow;
                        break;
                    case "red":
                        color = Color.red;
                        break;
                    default:
                        color = Color.black;
                        break;
                }

                WireframeBoxes.Draw(signal.gameObject.transform.localToWorldMatrix, signal.boundOffsets, signal.boundScale, color);
            }
        }

        public override void OnVisualizeToggle(bool state) {}
    }
}
