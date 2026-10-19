using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEngine;

// Debug tool. Records one row per physics tick to Logs/catch_<label>.csv. Add it to Player at runtime and
// call Record(label); it stops and writes on its own. World Z is forward, X right. Ground forces come from
// Physics.ContactEvent on the two foot colliders.
[DefaultExecutionOrder(5)]
public class CatchRecorder : MonoBehaviour
{
    [SerializeField] float duration = 3f;

    BalanceSensor balanceSensor;
    BalanceController balanceController;
    PlayerController playerController;
    PlayerRig playerRig;
    FootPlacement leftFoot, rightFoot;
    Rigidbody pelvis, torso, leftFootBody, rightFootBody;
    Transform ghostFootL, ghostFootR, ghostThighL, ghostShinL, thighL, shinL, footL;
    Transform ghostThighR, ghostShinR, thighR, shinR, footR;
    Transform upperArmL, upperArmR, forearmL, forearmR, head;
    ConfigurableJoint hipL, hipR, kneeL, kneeR, ankleL, ankleR;
    Collider soleL, soleR;

    // Ground impulse on each foot since the last FixedUpdate.
    Vector3 leftImpulse, rightImpulse;

    StringBuilder rows;
    string label;
    float startTime;

    public bool IsRecording => rows != null;

    void Awake()
    {
        balanceSensor = GetComponent<BalanceSensor>();
        balanceController = GetComponent<BalanceController>();
        playerController = GetComponent<PlayerController>();
        playerRig = GetComponent<PlayerRig>();

        foreach (FootPlacement foot in GetComponentsInChildren<FootPlacement>())
            if (foot.name.StartsWith("L")) leftFoot = foot; else rightFoot = foot;

        foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>())
        {
            switch (body.name)
            {
                case "Pelvis": pelvis = body; break;
                case "Torso": torso = body; break;
                case "Foot_L": leftFootBody = body; break;
                case "Foot_R": rightFootBody = body; break;
            }
        }

        ReportContacts(leftFootBody);
        ReportContacts(rightFootBody);

        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            switch (t.name)
            {
                case "GhostFoot_L": ghostFootL = t; break;
                case "GhostFoot_R": ghostFootR = t; break;
                case "GhostThigh_L": ghostThighL = t; break;
                case "GhostShin_L": ghostShinL = t; break;
                case "Shin_L": shinL = t; kneeL = t.GetComponent<ConfigurableJoint>(); break;
                case "Foot_L": footL = t; break;
                case "Thigh_L": thighL = t; hipL = t.GetComponent<ConfigurableJoint>(); break;
                case "Thigh_R": thighR = t; hipR = t.GetComponent<ConfigurableJoint>(); break;
                case "GhostThigh_R": ghostThighR = t; break;
                case "GhostShin_R": ghostShinR = t; break;
                case "Shin_R": shinR = t; kneeR = t.GetComponent<ConfigurableJoint>(); break;
                case "Foot_R": footR = t; break;
                case "UpperArm_L": upperArmL = t; break;
                case "UpperArm_R": upperArmR = t; break;
                case "Forearm_L": forearmL = t; break;
                case "Forearm_R": forearmR = t; break;
                case "Head": head = t; break;
            }
        }
        ankleL = leftFootBody.GetComponent<ConfigurableJoint>();
        ankleR = rightFootBody.GetComponent<ConfigurableJoint>();
        soleL = leftFootBody.GetComponent<Collider>();
        soleR = rightFootBody.GetComponent<Collider>();
    }

    // Contact reporting is fixed when a collider pair is created, so a foot already on the ground would
    // never report. Toggling the collider rebuilds its pairs without a physics step in between.
    static void ReportContacts(Rigidbody body)
    {
        foreach (Collider c in body.GetComponentsInChildren<Collider>())
        {
            c.providesContacts = true;
            c.enabled = false;
            c.enabled = true;
        }
    }

    void OnEnable() => Physics.ContactEvent += OnContacts;
    void OnDisable() => Physics.ContactEvent -= OnContacts;

    void OnContacts(PhysicsScene scene, NativeArray<ContactPairHeader>.ReadOnly headers)
    {
        foreach (ContactPairHeader header in headers)
        {
            bool left = header.body == leftFootBody || header.otherBody == leftFootBody;
            bool right = header.body == rightFootBody || header.otherBody == rightFootBody;
            if (!left && !right) continue;

            for (int i = 0; i < header.pairCount; i++)
            {
                ref readonly ContactPair pair = ref header.GetContactPair(i);
                if (!pair.collider.CompareTag("Ground") && !pair.otherCollider.CompareTag("Ground")) continue;

                // The impulse's sign depends on pair order; on flat ground the push on the foot is +y.
                Vector3 impulse = pair.impulseSum;
                if (impulse.y < 0f) impulse = -impulse;

                if (left) leftImpulse += impulse; else rightImpulse += impulse;
            }
        }
    }

    // seconds > 0 overrides the serialized duration.
    public void Record(string recordingLabel, float seconds = 0f)
    {
        if (seconds > 0f) duration = seconds;
        label = recordingLabel;
        startTime = Time.time;
        leftImpulse = rightImpulse = Vector3.zero;
        rows = new StringBuilder(
            "t,comZ,comVelX,comVelZ,captureX,captureZ,stanceZ,pelvisPitch,pelvisPitchRate,torsoPitch," +
            "ankleTrim,lStepping,lGroundZ,lFx,lFy,lFz,rStepping,rGroundZ,rFx,rFy,rFz," +
            "hipSag,lTargetAnkleY,lGhostAnkleY,lAnkleY,lSoleY,rTargetAnkleY,rGhostAnkleY,rAnkleY,rSoleY," +
            "lGhostThighPitch,lThighPitch,lGhostKnee,lKnee,lGhostFootPitch,lFootPitch,lTrackErr,rTrackErr," +
            "lAnkleZ,rAnkleZ,comY,pelvisY,lKneeFlex,rKneeFlex,lKneeOffAxis,rKneeOffAxis,lKneeGap,rKneeGap," +
            "pelvisYaw,lFootYaw,rFootYaw,lTargetYaw,rTargetYaw,ankleRoll,comX," +
            "pelvisYawRate,torsoYawRate,lThighYawRate,lShinYawRate,lFootYawRate,rFootYawRate," +
            "yawMomentum,trunkYawMomentum,lLegYawMomentum,rLegYawMomentum,uprightYawTorque," +
            "lHipTwist,lHipTwistTarget,rHipTwist,rHipTwistTarget,lAnkleTwist,lAnkleTwistTarget," +
            "rAnkleTwist,rAnkleTwistTarget,lGhostHipTwist,rGhostHipTwist," +
            "lGroundX,rGroundX,swayX,swayZ,pelvisRoll,momentumX,momentumY,fallLimit,breach,fallen,pelvisTilt," +
            "momentumCharge,tiltCharge,dropCharge,torsoRoll,lCrossing,rCrossing," +
            "lArmOut,lArmFwd,lElbow,rArmOut,rArmFwd,rElbow,headPitch,headRoll,headYaw\n");
    }

    void FixedUpdate()
    {
        Vector3 leftForce = leftImpulse / Time.fixedDeltaTime;
        Vector3 rightForce = rightImpulse / Time.fixedDeltaTime;
        leftImpulse = rightImpulse = Vector3.zero;

        if (rows == null) return;

        Vector3 com = balanceSensor.CenterOfMass;
        Vector3 comVelocity = balanceSensor.CenterOfMassVelocity;
        Vector3 capture = balanceSensor.CapturePoint;
        Quaternion ghostHips = playerRig.GhostHips.rotation;

        Append(Time.time - startTime, com.z, comVelocity.x, comVelocity.z, capture.x, capture.z,
               balanceController.StanceCentre().z,
               Pitch(pelvis.rotation), PitchRate(pelvis), Pitch(torso.rotation),
               SignedAngle(balanceController.AnkleTrim.eulerAngles.x),
               leftFoot.IsStepping ? 1 : 0, leftFoot.GroundPoint.z, leftForce.x, leftForce.y, leftForce.z,
               rightFoot.IsStepping ? 1 : 0, rightFoot.GroundPoint.z, rightForce.x, rightForce.y, rightForce.z,
               // Ghost hips above the real ones, unclamped.
               playerRig.GhostHips.position.y - (Anchor(hipL).y + Anchor(hipR).y) * 0.5f,
               leftFoot.transform.position.y, ghostFootL.position.y, Anchor(ankleL).y, soleL.bounds.min.y,
               rightFoot.transform.position.y, ghostFootR.position.y, Anchor(ankleR).y, soleR.bounds.min.y,
               // Left leg, ghost against physical.
               Pitch(ghostThighL.rotation), Pitch(thighL.rotation),
               Quaternion.Angle(ghostThighL.rotation, ghostShinL.rotation), Quaternion.Angle(thighL.rotation, shinL.rotation),
               Pitch(ghostFootL.rotation), Pitch(footL.rotation),
               TrackingError(ghostThighL, thighL, ghostShinL, shinL, ghostFootL, footL),
               TrackingError(ghostThighR, thighR, ghostShinR, shinR, ghostFootR, footR),
               Anchor(ankleL).z, Anchor(ankleR).z,
               com.y, pelvis.position.y,
               KneeFlexion(thighL, shinL), KneeFlexion(thighR, shinR),
               KneeOffAxis(thighL, shinL), KneeOffAxis(thighR, shinR),
               JointGap(kneeL) * 1000f, JointGap(kneeR) * 1000f,
               Yaw(pelvis.rotation), Yaw(footL.rotation), Yaw(footR.rotation),
               Yaw(leftFoot.transform.rotation), Yaw(rightFoot.transform.rotation),
               SignedAngle(balanceController.AnkleTrim.eulerAngles.z), com.x,
               YawRate(pelvis), YawRate(torso), YawRate(thighL), YawRate(shinL), YawRate(leftFootBody),
               YawRate(rightFootBody),
               // Whole body, trunk, each leg. Only an outside torque changes the whole.
               YawMomentum(com, pelvis, torso, thighL, shinL, footL, thighR, shinR, footR),
               YawMomentum(com, pelvis, torso),
               YawMomentum(com, thighL, shinL, footL),
               YawMomentum(com, thighR, shinR, footR),
               balanceController.UprightYawTorque,
               // Twist about each joint's Y: actual, then the drive's target.
               JointRotation(hipL).AngleAboutY(), Quaternion.Inverse(hipL.targetRotation).AngleAboutY(),
               JointRotation(hipR).AngleAboutY(), Quaternion.Inverse(hipR.targetRotation).AngleAboutY(),
               JointRotation(ankleL).AngleAboutY(), Quaternion.Inverse(ankleL.targetRotation).AngleAboutY(),
               JointRotation(ankleR).AngleAboutY(), Quaternion.Inverse(ankleR.targetRotation).AngleAboutY(),
               // What the IK solved: ghost thigh twist against the ghost hips.
               (Quaternion.Inverse(ghostHips) * ghostThighL.rotation).AngleAboutY(),
               (Quaternion.Inverse(ghostHips) * ghostThighR.rotation).AngleAboutY(),
               leftFoot.GroundPoint.x, rightFoot.GroundPoint.x,
               playerController.SwayWorld.x, playerController.SwayWorld.z,
               Roll(pelvis),
               playerController.Momentum.x, playerController.Momentum.y, playerController.MomentumLimit,
               playerController.FallMeterFraction, playerController.IsFallen ? 1 : 0,
               playerController.PelvisTilt,
               playerController.MomentumCharge, playerController.TiltCharge, playerController.DropCharge,
               Roll(torso),
               leftFoot.IsCrossing ? 1 : 0, rightFoot.IsCrossing ? 1 : 0,
               ArmOut(upperArmL, -1f), ArmForward(upperArmL), ElbowBend(upperArmL, forearmL),
               ArmOut(upperArmR, 1f), ArmForward(upperArmR), ElbowBend(upperArmR, forearmR),
               HeadPitch(), HeadRoll(), HeadYaw());

        if (Time.time - startTime >= duration)
            Write();
    }

    // --- Measurements, degrees unless noted ---

    // Upper arm's long axis in the heading's frame.
    Vector3 ArmDirection(Transform upperArm) => ToHeadingFrame(-upperArm.up);

    float ArmOut(Transform upperArm, float side)
    {
        Vector3 d = ArmDirection(upperArm);
        return Mathf.Atan2(side * d.x, -d.y) * Mathf.Rad2Deg;
    }

    float ArmForward(Transform upperArm)
    {
        Vector3 d = ArmDirection(upperArm);
        return Mathf.Atan2(d.z, -d.y) * Mathf.Rad2Deg;
    }

    static float ElbowBend(Transform upperArm, Transform forearm) => Vector3.Angle(-upperArm.up, -forearm.up);

    float HeadPitch()
    {
        Vector3 f = ToHeadingFrame(head.forward);
        return Mathf.Atan2(-f.y, new Vector2(f.x, f.z).magnitude) * Mathf.Rad2Deg;
    }

    float HeadRoll()
    {
        Vector3 u = ToHeadingFrame(head.up);
        return Mathf.Atan2(u.x, u.y) * Mathf.Rad2Deg;
    }

    float HeadYaw()
    {
        Vector3 f = ToHeadingFrame(head.forward);
        return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
    }

    Vector3 ToHeadingFrame(Vector3 world) => Quaternion.Inverse(playerController.Heading) * world;

    // A joint's world position, from its connected body's side.
    static Vector3 Anchor(ConfigurableJoint joint) =>
        joint.connectedBody.transform.TransformPoint(joint.connectedAnchor);

    // Mean angle between each ghost bone and its physical body. Includes the ankle trim, which the ghost
    // doesn't know about.
    static float TrackingError(Transform ghostThigh, Transform thigh, Transform ghostShin, Transform shin,
                               Transform ghostFoot, Transform foot) =>
        (Quaternion.Angle(ghostThigh.rotation, thigh.rotation) +
         Quaternion.Angle(ghostShin.rotation, shin.rotation) +
         Quaternion.Angle(ghostFoot.rotation, foot.rotation)) / 3f;

    // + bent forward, − hyperextended.
    static float KneeFlexion(Transform thigh, Transform shin) =>
        (Quaternion.Inverse(thigh.rotation) * shin.rotation).AngleAboutX();

    // The knee's rotation left once its bend about X is removed: twist and sideways fold.
    static float KneeOffAxis(Transform thigh, Transform shin)
    {
        Quaternion local = Quaternion.Inverse(thigh.rotation) * shin.rotation;
        Quaternion bend = Quaternion.AngleAxis(KneeFlexion(thigh, shin), Vector3.right);
        return Quaternion.Angle(Quaternion.identity, local * Quaternion.Inverse(bend));
    }

    // Distance the joint has pulled apart, m.
    static float JointGap(ConfigurableJoint joint) =>
        Vector3.Distance(joint.transform.TransformPoint(joint.anchor), Anchor(joint));

    // A joint's body relative to its connected body, from the physics poses.
    static Quaternion JointRotation(ConfigurableJoint joint) =>
        Quaternion.Inverse(joint.connectedBody.rotation) * joint.GetComponent<Rigidbody>().rotation;

    // + leaning forward.
    static float Pitch(Quaternion rotation) => SignedAngle(rotation.eulerAngles.x);
    // + leaning toward world +X.
    static float Roll(Rigidbody body) => Mathf.Asin(Mathf.Clamp((body.rotation * Vector3.up).x, -1f, 1f)) * Mathf.Rad2Deg;
    // + turned right.
    static float Yaw(Quaternion rotation)
    {
        Vector3 forward = rotation * Vector3.forward;
        return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
    }

    // Degrees per second.
    static float YawRate(Rigidbody body) => body.angularVelocity.y * Mathf.Rad2Deg;
    static float YawRate(Transform body) => YawRate(body.GetComponent<Rigidbody>());
    static float PitchRate(Rigidbody body) => body.angularVelocity.x * Mathf.Rad2Deg;
    static float SignedAngle(float degrees) => Mathf.DeltaAngle(0f, degrees);

    // Orbital (m · r × v) plus spin (I ω) about the vertical through `about`, kg·m²/s, + turning right.
    static float YawMomentum(Vector3 about, params Component[] bodies)
    {
        float total = 0f;
        foreach (Component c in bodies)
        {
            Rigidbody body = c.GetComponent<Rigidbody>();
            Vector3 r = body.worldCenterOfMass - about;
            total += body.mass * Vector3.Cross(r, body.linearVelocity).y;

            Quaternion axes = body.rotation * body.inertiaTensorRotation;
            Vector3 local = Quaternion.Inverse(axes) * body.angularVelocity;
            total += (axes * Vector3.Scale(body.inertiaTensor, local)).y;
        }
        return total;
    }

    void Append(params float[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) rows.Append(',');
            rows.Append(values[i].ToString("F3", CultureInfo.InvariantCulture));
        }
        rows.Append('\n');
    }

    void Write()
    {
        string path = Path.Combine(Application.dataPath, "..", "Logs", $"catch_{label}.csv");
        File.WriteAllText(path, rows.ToString());
        Debug.Log($"CatchRecorder: wrote {Path.GetFullPath(path)}", this);
        rows = null;
    }
}
