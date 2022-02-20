using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sylph.Unity {
    [RequireComponent(typeof(Animator))]
    public class NetworkAnimator : NetworkComponentBehaviour {
        public class AnimatorBoolSyncVar : BoolSyncVar {
            private Animator animator;
            private string name;
            
            public AnimatorBoolSyncVar(Animator animator, string name, bool value) : base(value) {
                this.animator = animator;
                this.name = name;
                ValueChanged += UpdateAnimatorBool;
            }

            private void UpdateAnimatorBool(bool oldValue, bool newValue) {
                animator.SetBool(name, newValue);
            }
        }

        public class AnimatorTriggerSyncVar : BoolSyncVar {
            private Animator animator;
            private string name;

            public AnimatorTriggerSyncVar(Animator animator, string name, bool value) : base(value) {
                this.animator = animator;
                this.name = name;
                ValueChanged += SetAnimatorTrigger;
            }

            private void SetAnimatorTrigger(bool oldValue, bool newValue) {
                if (newValue)
                    animator.SetTrigger(name);
            }
        }

        private Animator animator;

        private Dictionary<string, AnimatorBoolSyncVar> bools = new Dictionary<string, AnimatorBoolSyncVar>();
        private Dictionary<string, AnimatorTriggerSyncVar> triggers = new Dictionary<string, AnimatorTriggerSyncVar>();

        public override SyncVarBase[] InitSyncVars() {
            animator = GetComponent<Animator>();
            var boolParams = animator.parameters.Where(
                (param) => param.type == AnimatorControllerParameterType.Bool);
            var triggerParams = animator.parameters.Where(
                (param) => param.type == AnimatorControllerParameterType.Trigger);

            var syncVars = new List<SyncVarBase>();
            foreach (var param in boolParams) {
                var syncVar = new AnimatorBoolSyncVar(animator, param.name, param.defaultBool);
                bools[param.name] = syncVar;
                syncVars.Add(syncVar);
            }

            foreach (var param in triggerParams) {
                var syncVar = new AnimatorTriggerSyncVar(animator, param.name, false);
                triggers[param.name] = syncVar;
                syncVars.Add(syncVar);
            }

            return syncVars.ToArray();
        }

        public void SetBool(string name, bool value) {
            bools[name].Value = value;
        }

        public void SetTrigger(string name) {
            triggers[name].Value = true;
            triggers[name].IsDirty = true;
        }
    }
}
