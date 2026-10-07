using System;
using Mirror;
using Extensions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GWYF_CasinoChaos
{
    // Ordinary local MonoBehaviour: never a NetworkBehaviour or network object.
    [DefaultExecutionOrder(1000)]
    internal sealed class DongVisualController : MonoBehaviour
    {
        private PlayerController _owner;
        private PlayerProfile _profile;
        private Transform _body;
        private MeshRenderer _bodyRenderer, _renderer;
        private GameObject _rig;
        private Rigidbody _anchor, _pendulum;
        private HingeJoint _hinge;
        private Material _material;
        private Mesh _mesh;
        private GameObject _leftLeg, _rightLeg;
        private MeshRenderer _leftLegRenderer, _rightLegRenderer;
        private Material _legMaterial;
        private Transform _head;
        private MeshRenderer _headRenderer;
        private GameObject _leftEar,_rightEar,_leftCheek,_rightCheek;
        private GameObject _nub;
        private MeshRenderer _nubRenderer;
        private bool _leftLegPresent, _rightLegPresent;
        private static readonly Color StumpColor = new Color(.65f,.012f,.02f,1f);
        private static readonly Vector3 LegSize = new Vector3(.23f,.25f,.23f);
        private bool _disposed, _appearanceKnown;
        private uint _roll;
        private ulong _appearanceId;
        private DongAppearance _appearance;
        private GameObject _firstPersonBody;
        private MeshRenderer _firstPersonRenderer;
        private readonly MaterialPropertyBlock _bodyProperties = new MaterialPropertyBlock();
        private Vector3 Size => new Vector3(_appearance.Width, _appearance.Length, _appearance.Depth);
        private Vector3 CenterOfMass => new Vector3(0, -_appearance.Length / 2, 0);
        private Vector3 Inertia => DongVisualTuning.Mass / 12 * new Vector3(
            _appearance.Length * _appearance.Length + _appearance.Depth * _appearance.Depth,
            _appearance.Width * _appearance.Width + _appearance.Depth * _appearance.Depth,
            _appearance.Width * _appearance.Width + _appearance.Length * _appearance.Length);
        internal bool Disposed => _disposed;

        internal void Initialize(PlayerController owner)
        {
            _owner = owner; _profile = owner.GetComponent<PlayerProfile>();
            BindBody(); CreateFirstPersonBody(); RefreshState();
        }
        private void BindBody()
        {
            _body = _owner.transform.Find(DongVisualTuning.BodyPath);
            _bodyRenderer = _body ? _body.Find(DongVisualTuning.MeshPath)?.GetComponent<MeshRenderer>() : null;
            if (!_body || !_bodyRenderer || !_bodyRenderer.sharedMaterial)
                throw new InvalidOperationException("Confirmed Player/Model/Body/BodyModel/BodyMesh attachment/material missing.");
        }
        internal void RefreshState()
        {
            if (_disposed || !_owner || !_profile) return;
            if (!_body || !_bodyRenderer) { RemoveNub(); RemoveAccessories(); RemoveLegs(); RemoveFirstPersonBody(); BindBody(); CreateFirstPersonBody(); }
            uint roll = DongAppearanceNetwork.Roll(_profile.steamId);
            if (!_appearanceKnown || roll != _roll || _appearanceId != _profile.steamId)
            {
                _appearanceKnown = true; _roll = roll; _appearanceId = _profile.steamId;
                _appearance = DongAppearance.For(_profile.steamId,roll); RemoveRig();
            }
            var state = BodyPartState.Get(_profile.steamId);
            RefreshLegs(state); RefreshAccessories(state);
            // Wait for synchronized state rather than rendering a guessed default
            // during authentication/snapshot delivery.
            bool present = NetworkClient.active && _owner.gameObject.activeInHierarchy && _profile.steamId != 0 &&
                state.Revision != 0 && state.Has(CustomBodyPart.Dong);
            bool known=NetworkClient.active && _profile.steamId!=0 && state.Revision!=0;
            if (!present) { RemoveRig(); if(known)CreateNub();else RemoveNub(); return; }
            RemoveNub();
            if (!_rig) { RemoveRig(); CreateRig(); }
            else if (!_rig.activeSelf)
            {
                Rebase(Pivot(), _body.rotation);
                _rig.SetActive(true);
            }
        }
        private void CreateRig()
        {
            if (!_body || !_bodyRenderer) { RemoveFirstPersonBody(); BindBody(); CreateFirstPersonBody(); }
            int layer = LayerMask.NameToLayer("Player");
            if (layer < 0) throw new InvalidOperationException("Confirmed visible Player layer missing.");
            _rig = new GameObject("CasinoChaosDongRig_" + _owner.netId);
            _rig.SetActive(false);
            if (_owner.gameObject.scene.IsValid() && _owner.gameObject.scene.isLoaded)
                SceneManager.MoveGameObjectToScene(_rig, _owner.gameObject.scene);
            // A separate scene root prevents player transforms from teleporting
            // a dynamic child each frame, and keeps cosmetic Rigidbodies out of
            // vanilla player hierarchy queries. Only this kinematic anchor follows
            // BodyModel; the joint never connects to the player's Rigidbody.
            var anchorObject = new GameObject("DongAnchor");
            anchorObject.transform.SetParent(_rig.transform, false);
            _anchor = anchorObject.AddComponent<Rigidbody>();
            _anchor.isKinematic = true; _anchor.useGravity = false;
            _anchor.detectCollisions = false; _anchor.interpolation = RigidbodyInterpolation.Interpolate;
            _anchor.position = Pivot(); _anchor.rotation = _body.rotation;

            var pendulumObject = new GameObject("DongVisual");
            pendulumObject.transform.SetParent(_rig.transform, false);
            pendulumObject.layer = layer;
            _pendulum = pendulumObject.AddComponent<Rigidbody>();
            _pendulum.position = _anchor.position; _pendulum.rotation = _anchor.rotation;
            _pendulum.mass = DongVisualTuning.Mass; _pendulum.useGravity = true;
            _pendulum.linearDamping = _appearance.Length < .11f ? .08f : DongVisualTuning.LinearDamping;
            _pendulum.angularDamping = _appearance.AngularDamping;
            _pendulum.maxAngularVelocity = _appearance.MaximumAngularSpeed;
            _pendulum.interpolation = RigidbodyInterpolation.Interpolate;
            _pendulum.detectCollisions = false;
            _pendulum.solverIterations = 8; _pendulum.solverVelocityIterations = 4;

            var mesh = new GameObject("HangingModel", typeof(MeshFilter), typeof(MeshRenderer));
            _mesh = DongVisualMesh.Create(_appearance.BroadTip); mesh.GetComponent<MeshFilter>().sharedMesh = _mesh;
            mesh.name = "HangingModel"; mesh.transform.SetParent(pendulumObject.transform, false);
            mesh.layer = layer; mesh.transform.localPosition = CenterOfMass;
            mesh.transform.localScale = Size;
            // There is no collision participation, including raycast collateral.
            // No ignore-pair bookkeeping or collision layers need restoration.
            _renderer = mesh.GetComponent<MeshRenderer>();
            _material = new Material(_bodyRenderer.sharedMaterial) { name = "CasinoChaosDongMaterial" };
            if (_material.HasProperty("_MainTex")) _material.SetTexture("_MainTex", Texture2D.whiteTexture);
            if (_material.HasProperty("_BaseMap")) _material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            _renderer.sharedMaterial = _material;
            // Without colliders Unity cannot infer mass distribution.
            // Use the conservative bounding prism for this small faceted cosmetic.
            // Explicit COM below the pivot is essential for actual gravity torque.
            _pendulum.centerOfMass = CenterOfMass;
            _pendulum.inertiaTensorRotation = Quaternion.identity;
            _pendulum.inertiaTensor = Inertia;

            _hinge = pendulumObject.AddComponent<HingeJoint>();
            _hinge.connectedBody = _anchor;
            _hinge.autoConfigureConnectedAnchor = false;
            _hinge.anchor = _hinge.connectedAnchor = Vector3.zero;
            _hinge.axis = _appearance.Sideways ? Vector3.forward : Vector3.right;
            _hinge.useMotor = false; _hinge.useSpring = false; _hinge.enableCollision = false;
            _hinge.limits = new JointLimits {
                min = -DongVisualTuning.SwingLimitDegrees, max = DongVisualTuning.SwingLimitDegrees,
                bounciness = 0, bounceMinVelocity = 0, contactDistance = 3
            };
            _hinge.useLimits = !_appearance.FullRotation;
            _rig.SetActive(true);
            CasinoChaosPlugin.Log($"Dong visual created: {HeatSystem.Identity(_owner)}; attachment=Player/{DongVisualTuning.BodyPath}; " +
                $"offset={AttachmentOffset()}; size={Size}; fullRotation={_appearance.FullRotation}; broadTip={_appearance.BroadTip}; hinge={(_appearance.Sideways ? "Z (side to side)" : "X (forward/back)")} {(_appearance.FullRotation ? "unlimited" : "+/-" + DongVisualTuning.SwingLimitDegrees)}; local cosmetic physics; no colliders/player connection.");
        }
        private Vector3 AttachmentOffset() => DongVisualTuning.AttachmentOffset + Vector3.forward * _appearance.AttachmentDepth;
        private Vector3 Pivot() => _body.TransformPoint(AttachmentOffset());
        private void FixedUpdate()
        {
            if (_disposed || !_rig || !_anchor || !_pendulum || !_owner) return;
            if (!_body || !_bodyRenderer)
            {
                // A replaced model must not leave a rig at its last world pose.
                RemoveRig();
                try { RemoveNub(); RemoveAccessories(); RemoveLegs(); RemoveFirstPersonBody(); BindBody(); CreateFirstPersonBody(); RefreshState(); }
                catch (Exception e) { CasinoChaosPlugin.Log("Dong attachment rebind failed: " + e.Message); DongVisuals.MarkFailed(_owner); Dispose(); }
                return;
            }
            Vector3 position = Pivot(); Quaternion rotation = _body.rotation;
            if ((position - _anchor.position).sqrMagnitude > DongVisualTuning.TeleportDistance * DongVisualTuning.TeleportDistance)
            {
                // A teleport/respawn is not a physical impulse. Rebase only the
                // isolated cosmetic rig; never write to the player's physics.
                Rebase(position, rotation);
            }
            else
            {
                bool moving = (position - _anchor.position).sqrMagnitude > .000001f || Quaternion.Angle(rotation, _anchor.rotation) > .05f;
                _anchor.MovePosition(position); _anchor.MoveRotation(rotation);
                if (moving) _pendulum.WakeUp();
            }
        }
        private void Rebase(Vector3 position, Quaternion rotation)
        {
            _anchor.position = position; _anchor.rotation = rotation;
            _pendulum.position = position; _pendulum.rotation = rotation;
            _pendulum.linearVelocity = _pendulum.angularVelocity = Vector3.zero;
        }
        private void CreateFirstPersonBody()
        {
            if (!_owner.isLocalPlayer || _firstPersonBody || !_bodyRenderer) return;
            var source = _bodyRenderer.GetComponent<MeshFilter>();
            if (!source || !source.sharedMesh) return;
            _firstPersonBody = new GameObject("CasinoChaosFirstPersonBody", typeof(MeshFilter), typeof(MeshRenderer));
            _firstPersonBody.transform.SetParent(_bodyRenderer.transform,false);
            _firstPersonBody.layer = LayerMask.NameToLayer("Player");
            _firstPersonBody.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            _firstPersonRenderer = _firstPersonBody.GetComponent<MeshRenderer>();
            _firstPersonRenderer.sharedMaterials = _bodyRenderer.sharedMaterials;
            _firstPersonRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _firstPersonRenderer.enabled = false;
        }
        private void RemoveFirstPersonBody()
        {
            if (_firstPersonBody) { _firstPersonBody.SetActive(false); Destroy(_firstPersonBody); }
            _firstPersonBody=null; _firstPersonRenderer=null;
        }
        private void LateUpdate()
        {
            if (_firstPersonRenderer && _bodyRenderer)
            {
                var local = MonoSingleton<LocalManager>.Instance;
                var camera = local ? local.mainCamera : null;
                bool firstPerson = IsFirstPerson();
                _firstPersonBody.transform.localPosition = firstPerson
                    ? _bodyRenderer.transform.InverseTransformVector(_body.TransformVector(DongVisualTuning.FirstPersonAdjustment))
                    : Vector3.zero;
                // Show only the existing body-only mesh when the normal camera
                // culls it. No head/face, and no doubled mesh in Cameraman mode.
                _firstPersonRenderer.enabled = firstPerson && _bodyRenderer.enabled && camera &&
                    (camera.cullingMask & (1 << _bodyRenderer.gameObject.layer)) == 0;
                _bodyRenderer.GetPropertyBlock(_bodyProperties); _firstPersonRenderer.SetPropertyBlock(_bodyProperties);
            }
            UpdateLegPose(); UpdateNubPose(); RefreshAccessories(BodyPartState.Get(_profile.steamId));
            if (!_renderer || !_bodyRenderer || !_body || !_anchor || !_pendulum) return;
            _renderer.enabled = _bodyRenderer.enabled;
            // Physics advances at fixed intervals, while player interpolation and
            // BodyModel's yaw spring finish in the render frame. Stitch ONLY the
            // collider-free mesh to the current pivot; keep the simulated angle.
            // This never moves either Rigidbody, the joint, or the player.
            Quaternion relative = Quaternion.Inverse(_anchor.transform.rotation) * _pendulum.transform.rotation;
            Quaternion rotation = _body.rotation * relative;
            // A ten-degree silhouette bias opens a small gap below the embedded
            // root, without introducing a second physics swing axis or a motor.
            rotation *= Quaternion.Euler(DongVisualTuning.OutwardTiltDegrees,0,0);
            Vector3 pivot = Pivot();
            if (IsFirstPerson()) pivot += _body.TransformVector(DongVisualTuning.FirstPersonAdjustment);
            _renderer.transform.SetPositionAndRotation(pivot + rotation * CenterOfMass, rotation);
        }
        private void CreateNub()
        {
            if(!_nub)
            {
                _nub=GameObject.CreatePrimitive(PrimitiveType.Sphere);_nub.name="CasinoChaosDongNub";
                DestroyImmediate(_nub.GetComponent<Collider>());
                _nub.layer=LayerMask.NameToLayer("Player");_nub.transform.SetParent(_body,false);
                _nubRenderer=_nub.GetComponent<MeshRenderer>();_nubRenderer.sharedMaterial=_legMaterial;
            }
            UpdateNubPose();
        }
        private void UpdateNubPose()
        {
            if(!_nubRenderer||!_bodyRenderer||!_body)return;
            Vector3 pose=AttachmentOffset()+new Vector3(0,-_appearance.Length*.035f,-_appearance.Depth*.10f-.006f);
            if(IsFirstPerson())pose+=DongVisualTuning.FirstPersonAdjustment;
            _nub.transform.localPosition=pose;
            _nub.transform.localScale=new Vector3(_appearance.Width*.42f,_appearance.Length*.12f,_appearance.Depth*.42f);
            _nubRenderer.enabled=_bodyRenderer.enabled;
            _bodyRenderer.GetPropertyBlock(_bodyProperties);
            _bodyProperties.SetColor("_Color",StumpColor);
            if(_legMaterial.HasProperty("_BaseColor"))_bodyProperties.SetColor("_BaseColor",StumpColor);
            _nubRenderer.SetPropertyBlock(_bodyProperties);
        }
        private void RemoveNub()
        {
            if(_nub){_nub.SetActive(false);Destroy(_nub);}_nub=null;_nubRenderer=null;
        }
        private void RefreshAccessories(BodyState state)
        {
            if(!_body||!_bodyRenderer||!_profile)return;
            if(!NetworkClient.active||_profile.steamId==0||state.Revision==0){RemoveAccessories();return;}
            if(!_head){_head=_owner.transform.Find("Model/PlayerHead/HeadModel");_headRenderer=_head?_head.GetComponent<MeshRenderer>():null;}
            if(_head&&_headRenderer)
            {
                bool show=!IsFirstPerson()&&_headRenderer.enabled;
                Ear(ref _leftEar,state.Has(CustomBodyPart.LeftEar),-1,show);
                Ear(ref _rightEar,state.Has(CustomBodyPart.RightEar),1,show);
            }
            bool butt=state.Has(CustomBodyPart.Butt);
            Vector3 adjustment=IsFirstPerson()?DongVisualTuning.FirstPersonAdjustment:Vector3.zero;
            // Recessed red caps occupy the cheek attachment sites when Butt is missing.
            var cheekSize=butt?new Vector3(.22f,.25f,.18f):new Vector3(.14f,.17f,.08f);
            float cheekDepth=butt?-.32f:-.292f;
            Shape(ref _leftCheek,"LeftButtCheek",_body,new Vector3(-.10f,-.12f,cheekDepth)+adjustment,cheekSize,_bodyRenderer,!butt,_bodyRenderer.enabled);
            Shape(ref _rightCheek,"RightButtCheek",_body,new Vector3(.10f,-.12f,cheekDepth)+adjustment,cheekSize,_bodyRenderer,!butt,_bodyRenderer.enabled);
        }
        private void Ear(ref GameObject ear,bool present,float side,bool show)
        {
            Shape(ref ear,side<0?"LeftEar":"RightEar",_head,new Vector3(side*(present?.54f:.484f),-.025f,0),
                present?new Vector3(.12f,.22f,.12f):new Vector3(.09f,.13f,.065f),_headRenderer,!present,show);
        }
        private void Shape(ref GameObject shape,string name,Transform parent,Vector3 position,Vector3 size,MeshRenderer source,bool red,bool show)
        {
            if(!_legMaterial)return;
            if(!shape)
            {
                shape=GameObject.CreatePrimitive(PrimitiveType.Sphere);shape.name="CasinoChaos"+name;
                DestroyImmediate(shape.GetComponent<Collider>());shape.layer=LayerMask.NameToLayer("Player");shape.transform.SetParent(parent,false);
                shape.GetComponent<MeshRenderer>().sharedMaterial=_legMaterial;
            }
            shape.transform.localPosition=position;shape.transform.localScale=size;
            var renderer=shape.GetComponent<MeshRenderer>();renderer.enabled=show;
            source.GetPropertyBlock(_bodyProperties);
            if(red){_bodyProperties.SetColor("_Color",StumpColor);if(_legMaterial.HasProperty("_BaseColor"))_bodyProperties.SetColor("_BaseColor",StumpColor);}
            renderer.SetPropertyBlock(_bodyProperties);
        }
        private void RemoveShape(ref GameObject shape){if(shape){shape.SetActive(false);Destroy(shape);}shape=null;}
        private void RemoveAccessories(){RemoveShape(ref _leftEar);RemoveShape(ref _rightEar);RemoveShape(ref _leftCheek);RemoveShape(ref _rightCheek);_head=null;_headRenderer=null;}
        private void RefreshLegs(BodyState state)
        {
            bool known = NetworkClient.active && _profile.steamId != 0 && state.Revision != 0;
            if(!known){RemoveLegs();return;}
            _leftLegPresent=state.Has(CustomBodyPart.LeftLeg);
            _rightLegPresent=state.Has(CustomBodyPart.RightLeg);
            SetLeg(ref _leftLeg, ref _leftLegRenderer, "LeftLegOrb");
            SetLeg(ref _rightLeg, ref _rightLegRenderer, "RightLegOrb");
            UpdateLegPose();
        }
        private void SetLeg(ref GameObject leg, ref MeshRenderer renderer, string name)
        {
            if(leg)return;
            if(!_legMaterial)
            {
                _legMaterial=new Material(_bodyRenderer.sharedMaterial){name="CasinoChaosLegMaterial"};
                if(_legMaterial.HasProperty("_MainTex"))_legMaterial.SetTexture("_MainTex",Texture2D.whiteTexture);
                if(_legMaterial.HasProperty("_BaseMap"))_legMaterial.SetTexture("_BaseMap",Texture2D.whiteTexture);
            }
            leg=GameObject.CreatePrimitive(PrimitiveType.Sphere);leg.name="CasinoChaos"+name;
            DestroyImmediate(leg.GetComponent<Collider>());
            leg.layer=LayerMask.NameToLayer("Player");leg.transform.SetParent(_body,false);
            leg.transform.localPosition=Vector3.zero;leg.transform.localScale=LegSize;
            renderer=leg.GetComponent<MeshRenderer>();renderer.sharedMaterial=_legMaterial;
        }
        private void UpdateLegPose()
        {
            if(!_bodyRenderer||!_body)return;
            Vector3 adjustment=IsFirstPerson()?DongVisualTuning.FirstPersonAdjustment:Vector3.zero;
            ApplyLegPose(_leftLeg,_leftLegRenderer,_leftLegPresent,-1,adjustment);
            ApplyLegPose(_rightLeg,_rightLegRenderer,_rightLegPresent,1,adjustment);
        }
        private void ApplyLegPose(GameObject leg, MeshRenderer renderer, bool present, float side, Vector3 adjustment)
        {
            if(!leg||!renderer)return;
            // Recess the same orb rather than removing it, leaving a small cap.
            float x=present?.28f:.215f;
            float z=present?.10f:.08f;
            leg.transform.localPosition=new Vector3(side*x,-.285f,z)+adjustment;
            renderer.enabled=_bodyRenderer.enabled;
            // Start from skin properties separately for each leg. Overrides on
            // a missing leg cannot tint its owned neighbour or the body itself.
            _bodyRenderer.GetPropertyBlock(_bodyProperties);
            if(!present)
            {
                if(_legMaterial.HasProperty("_Color"))_bodyProperties.SetColor("_Color",StumpColor);
                if(_legMaterial.HasProperty("_BaseColor"))_bodyProperties.SetColor("_BaseColor",StumpColor);
            }
            renderer.SetPropertyBlock(_bodyProperties);
        }
        private void RemoveLegs()
        {
            if(_leftLeg){_leftLeg.SetActive(false);Destroy(_leftLeg);}
            if(_rightLeg){_rightLeg.SetActive(false);Destroy(_rightLeg);}
            _leftLeg=_rightLeg=null;_leftLegRenderer=_rightLegRenderer=null;
            if(_legMaterial)Destroy(_legMaterial);_legMaterial=null;
        }
        private bool IsFirstPerson()
        {
            if (!_owner || !_owner.isLocalPlayer) return false;
            var cameraman = _owner.GetComponent<CameramanMode>();
            var local = MonoSingleton<LocalManager>.Instance;
            return !(cameraman && cameraman.IsActive) && local && local.mainCamera && local.mainCamera.isActiveAndEnabled;
        }
        private void RemoveRig()
        {
            if (_rig)
            {
                _rig.SetActive(false);
                if (_hinge) _hinge.connectedBody = null;
                Destroy(_rig);
                CasinoChaosPlugin.Log("Dong visual removed: " + HeatSystem.Identity(_owner));
            }
            if (_material) Destroy(_material);
            if (_mesh) Destroy(_mesh); _mesh = null;
            _rig = null; _anchor = _pendulum = null; _hinge = null; _renderer = null; _material = null;
        }
        internal void Dispose()
        {
            if (_disposed) return;
            _disposed = true; enabled = false; RemoveRig(); RemoveNub(); RemoveAccessories(); RemoveLegs(); RemoveFirstPersonBody();
        }
        private void OnDisable() { if (_rig) _rig.SetActive(false); }
        private void OnDestroy() => Dispose();
    }
}
