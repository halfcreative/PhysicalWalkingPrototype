using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEngine;

// Debug tool. Records one row per physics tick for a few seconds and writes it to
// Logs/catch_<label>.csv, for taking a catch step apart after the fact. Add it to Player at runtime and
// call Record(label); it stops and writes on its own. Never in the scene.
//
// Columns are in the frame of the push: world Z is forward, X is right. Ground forces come from
// Physics.ContactEvent, with contact reporting switched on for the two foot colliders.
[DefaultExecutionOrder(5)]
public class CatchRecorder : MonoBehaviour
{
    [SerializeField] float duration = 3f;

    BalanceSensor sensor;
    BalanceController balance;
    FootPlacement leftFoot, rightFoot;
    Rigidbody pelvis, torso, leftFootBody, rightFootBody;
    PlayerRig rig;
    Transform ghostFootL, ghostFootR, ghostThighL, ghostShinL, thighL, shinL, footL;
    Transform ghostThighR, ghostShinR, thighR, shinR, footR;
    ConfigurableJoint hipL, hipR, kneeL, kneeR, ankleL, ankleR;
    Collider soleL, soleR;

    // Ground impulse on each foot, summed over the physics step since the last FixedUpdate.
    Vector3 leftImpulse, rightImpulse;

    StringBuilder rows;
    string label;
    float startTime;

    public bool IsRecording => rows != null;

    void Awake()
    {
        sensor = GetComponent<BalanceSensor>();
        balance = GetComponent<BalanceController>();

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

        rig = GetComponent<PlayerRig>();
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
            }
        }
        ankleL = leftFootBody.GetComponent<ConfigurableJoint>();
        ankleR = rightFootBody.GetComponent<ConfigurableJoint>();
        soleL = leftFootBody.GetComponent<Collider>();
        soleR = rightFootBody.GetComponent<Collider>();
    }

    // A joint's anchor on its parent, in world space: where the physical joint actually is.
    static Vector3 Anchor(ConfigurableJoint joint) =>
        joint.connectedBody.transform.TransformPoint(joint.connectedAnchor);

    // PhysX fixes a pair's reporting flags when the pair is created, so a foot already standing on the
    // ground when this is added would never report. Toggling the collider rebuilds its pairs; no physics
    // step runs in between, so the body doesn't notice.
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

                // The impulse's sign depends on which body PhysX listed first. The floor is flat, so the
                // ground's push on the foot always has +y: orient it that way.
                Vector3 j = pair.impulseSum;
                if (j.y < 0f) j = -j;

                if (left) leftImpulse += j; else rightImpulse += j;
            }
        }
    }

    public void Record(string recordingLabel)
    {
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
            "rAnkleTwist,rAnkleTwistTarget,lGhostHipTwist,rGhostHipTwist\n");
    }

    void FixedUpdate()
    {
        Vector3 lForce = leftImpulse / Time.fixedDeltaTime;
        Vector3 rForce = rightImpulse / Time.fixedDeltaTime;
        leftImpulse = rightImpulse = Vector3.zero;

        if (rows == null) return;

        Vector3 stance = leftFoot.IsStepping ? rightFoot.SupportPoint
                       : rightFoot.IsStepping ? leftFoot.SupportPoint
                       : (leftFoot.SupportPoint + rightFoot.SupportPoint) * 0.5f;

        Append(Time.time - startTime, sensor.CenterOfMass.z, sensor.CenterOfMassVelocity.x,
               sensor.CenterOfMassVelocity.z, sensor.CapturePoint.x, sensor.CapturePoint.z, stance.z,
               Pitch(pelvis), PitchRate(pelvis), Pitch(torso), SignedAngle(balance.AnkleTrim.eulerAngles.x),
               leftFoot.IsStepping ? 1 : 0, leftFoot.GroundPoint.z, lForce.x, lForce.y, lForce.z,
               rightFoot.IsStepping ? 1 : 0, rightFoot.GroundPoint.z, rForce.x, rForce.y, rForce.z,
               // How far the ghost hips sit above the real ones: the swing foot hangs this much low.
               rig.GhostHips.position.y - (Anchor(hipL).y + Anchor(hipR).y) * 0.5f,
               leftFoot.transform.position.y, ghostFootL.position.y, Anchor(ankleL).y, soleL.bounds.min.y,
               rightFoot.transform.position.y, ghostFootR.position.y, Anchor(ankleR).y, soleR.bounds.min.y,
               // Left leg, ghost against physical: thigh pitch in world, knee flexion, foot pitch in world.
               Pitch(ghostThighL.rotation), Pitch(thighL.rotation),
               Quaternion.Angle(ghostThighL.rotation, ghostShinL.rotation), Quaternion.Angle(thighL.rotation, shinL.rotation),
               Pitch(ghostFootL.rotation), Pitch(footL.rotation),
               TrackingError(ghostThighL, thighL, ghostShinL, shinL, ghostFootL, footL),
               TrackingError(ghostThighR, thighR, ghostShinR, shinR, ghostFootR, footR),
               // Where the physical ankles actually are, against lGroundZ / rGroundZ (the targets).
               Anchor(ankleL).z, Anchor(ankleR).z,
               // Heights, for telling a fall (body down) from a stumble it recovers from.
               sensor.CenterOfMass.y, pelvis.position.y,
               // Signed knee flexion: negative is bent backward.
               KneeFlexion(thighL, shinL), KneeFlexion(thighR, shinR),
               // How far the knee has turned off its hinge (twist or sideways fold), degrees, and how far
               // the joint has pulled apart, millimetres.
               KneeOffAxis(thighL, shinL), KneeOffAxis(thighR, shinR),
               JointGap(kneeL) * 1000f, JointGap(kneeR) * 1000f,
               // Heading in degrees, + turned right: the body, each physical foot, and each foot target.
               Yaw(pelvis.rotation), Yaw(footL.rotation), Yaw(footR.rotation),
               Yaw(leftFoot.transform.rotation), Yaw(rightFoot.transform.rotation),
               // Ankle roll trim (+ lifts the right edge) and the sideways position it acts on.
               SignedAngle(balance.AnkleTrim.eulerAngles.z), sensor.CenterOfMass.x,
               // World yaw rates, degrees/s, + turning right: where a turn starts and how it travels.
               YawRate(pelvis), YawRate(torso), YawRate(thighL), YawRate(shinL), YawRate(leftFootBody),
               YawRate(rightFootBody),
               // Yaw angular momentum about the centre of mass, kg·m²/s, + turning right: the whole body,
               // then the trunk and each leg. Only an outside torque (the ground, the upright torque)
               // changes the whole; a trunk turning while the whole holds is a reaction to a leg.
               YawMomentum(sensor.CenterOfMass, pelvis, torso, thighL, shinL, footL, thighR, shinR, footR),
               YawMomentum(sensor.CenterOfMass, pelvis, torso),
               YawMomentum(sensor.CenterOfMass, thighL, shinL, footL),
               YawMomentum(sensor.CenterOfMass, thighR, shinR, footR),
               balance.UprightYawTorque,
               // Twist about each joint's Y (the leg's long axis), degrees: where it is, and where the
               // drive is pulling it.
               Twist(JointRotation(hipL)), Twist(Quaternion.Inverse(hipL.targetRotation)),
               Twist(JointRotation(hipR)), Twist(Quaternion.Inverse(hipR.targetRotation)),
               Twist(JointRotation(ankleL)), Twist(Quaternion.Inverse(ankleL.targetRotation)),
               Twist(JointRotation(ankleR)), Twist(Quaternion.Inverse(ankleR.targetRotation)),
               // The same twist on the ghost, thigh against ghost hips: what the IK solved.
               Twist(Quaternion.Inverse(rig.GhostHips.rotation) * ghostThighL.rotation),
               Twist(Quaternion.Inverse(rig.GhostHips.rotation) * ghostThighR.rotation));

        if (Time.time - startTime >= duration)
            Write();
    }

    // Mean angle (degrees) between each ghost bone and the physical body it drives: whether a problem is
    // in the pose being asked for or in the drives' ability to reach it. Includes the ankle trim, which the
    // ghost doesn't know about, so a planted foot reads a few degrees even when it's doing its job.
    static float TrackingError(Transform ghostThigh, Transform thigh, Transform ghostShin, Transform shin,
                               Transform ghostFoot, Transform foot) =>
        (Quaternion.Angle(ghostThigh.rotation, thigh.rotation) +
         Quaternion.Angle(ghostShin.rotation, shin.rotation) +
         Quaternion.Angle(ghostFoot.rotation, foot.rotation)) / 3f;

    // Degrees of knee bend about the knee axis, + forward (normal), − backward. Twist about X only.
    static float KneeFlexion(Transform thigh, Transform shin)
    {
        Quaternion local = Quaternion.Inverse(thigh.rotation) * shin.rotation;
        if (local.w < 0f) local = new Quaternion(-local.x, -local.y, -local.z, -local.w);
        return 2f * Mathf.Atan2(local.x, local.w) * Mathf.Rad2Deg;
    }

    // The rest of the knee's rotation once its bend about X is taken out.
    static float KneeOffAxis(Transform thigh, Transform shin)
    {
        Quaternion local = Quaternion.Inverse(thigh.rotation) * shin.rotation;
        Quaternion bend = Quaternion.AngleAxis(KneeFlexion(thigh, shin), Vector3.right);
        return Quaternion.Angle(Quaternion.identity, local * Quaternion.Inverse(bend));
    }

    // Distance between a joint's anchor on its own body and on its connected body.
    static float JointGap(ConfigurableJoint joint) =>
        Vector3.Distance(joint.transform.TransformPoint(joint.anchor),
                         joint.connectedBody.transform.TransformPoint(joint.connectedAnchor));

    // Forward pitch in degrees: + leans forward.
    static float Pitch(Rigidbody body) => Pitch(body.rotation);
    static float Pitch(Quaternion rotation) => SignedAngle(rotation.eulerAngles.x);
    static float Yaw(Quaternion rotation)
    {
        Vector3 forward = rotation * Vector3.forward;
        return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
    }
    // A joint's body relative to its connected body: what the drive's target is compared against.
    static Quaternion JointRotation(ConfigurableJoint joint) =>
        Quaternion.Inverse(joint.connectedBody.rotation) * joint.GetComponent<Rigidbody>().rotation;

    static float Twist(Quaternion q)
    {
        if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
        return 2f * Mathf.Atan2(q.y, q.w) * Mathf.Rad2Deg;
    }

    // Orbital (m · r × v) plus spin (I ω) about the vertical through `about`.
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

    static float YawRate(Rigidbody body) => body.angularVelocity.y * Mathf.Rad2Deg;
    static float YawRate(Transform body) => YawRate(body.GetComponent<Rigidbody>());
    static float PitchRate(Rigidbody body) => body.angularVelocity.x * Mathf.Rad2Deg;
    static float SignedAngle(float degrees) => Mathf.DeltaAngle(0f, degrees);

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
