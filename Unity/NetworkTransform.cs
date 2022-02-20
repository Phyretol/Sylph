using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sylph.Unity {
    public class Vector3SyncVar : SyncVar<Vector3> {
        public Vector3SyncVar(Vector3 value) : base(value) { }

        public override void Read(MemoryStream stream) {
            BinaryReader reader = new BinaryReader(stream);
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();

            Value = new Vector3(x, y, z);
        }

        public override void Write(MemoryStream stream) {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(Value.x);
            writer.Write(Value.y);
            writer.Write(Value.z);
        }
    }

    public class NetworkTransform : NetworkComponentBehaviour {
        private Vector3SyncVar positionSync;
        private FloatSyncVar rotationSync;

        private float speedEstimate;
        private float positionTime;

        private float rotationSpeedEstimate;
        private float rotationTime;

        public float rotationThreshold = 90f;
        public float positionThreshold = 1f;

        public override SyncVarBase[] InitSyncVars() {
            positionSync = new Vector3SyncVar(transform.position);
            rotationSync = new FloatSyncVar(transform.eulerAngles.y);

            if (NetworkManager.Instance.isClientOnly) {
                positionTime = rotationTime = Time.time;
                speedEstimate = rotationSpeedEstimate = 0f;

                positionSync.ValueChanged += UpdatePositionEstimate;
                rotationSync.ValueChanged += UpdateRotationEstimate;
            }

            return new SyncVarBase[] {
                positionSync,
                rotationSync
            };
        }

        private void Update() {
            if (NetworkManager.Instance.isServer) {
                positionSync.Value = transform.position;
                rotationSync.Value = transform.eulerAngles.y;
            } else {
                Vector3 position = Vector3.MoveTowards(transform.position, positionSync.Value, speedEstimate * Time.deltaTime);
                float maxDeltaAngle = rotationSpeedEstimate * Time.deltaTime;
                Vector3 angles = transform.eulerAngles;
                if (Mathf.Abs(Mathf.DeltaAngle(angles.y, rotationSync.Value)) > Mathf.Abs(maxDeltaAngle))
                    angles.y += maxDeltaAngle;
                else
                    angles.y = rotationSync.Value;
                transform.eulerAngles = angles;
                transform.position = position;
            }
        }

        private void UpdatePositionEstimate(Vector3 oldValue, Vector3 newValue) {
            float delta = Time.time - positionTime;
            float deltaPosition = (newValue - oldValue).magnitude;
            if (delta > 0 && Vector3.Distance(transform.position, newValue) < positionThreshold) 
                speedEstimate = deltaPosition / delta;
            else 
                transform.position = newValue;
            
            positionTime = Time.time;
        }

        private void UpdateRotationEstimate(float oldValue, float newValue) {
            float delta = Time.time - rotationTime;
            float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(oldValue, newValue));
            if (delta > 0 && Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, newValue)) < rotationThreshold) {
                rotationSpeedEstimate = deltaAngle / delta;
                rotationSpeedEstimate *= Mathf.Sign(Mathf.DeltaAngle(transform.eulerAngles.y, newValue));
            } else {
                Vector3 angles = transform.eulerAngles;
                angles.y = newValue;
                transform.eulerAngles = angles;
            }
            rotationTime = Time.time;
        }
    }
}
