using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// Two camera views: a fixed overview of the whole intersection, and a chase view
    /// that follows the first scooter.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        private static readonly Vector3 OverviewPosition = new Vector3(0f, 82f, -84f);
        private static readonly Vector3 OverviewTarget = new Vector3(0f, 0f, -6f);

        private SimulationManager sim;

        public bool FollowScooter { get; private set; }

        /// <summary>Uses the scene's main camera if there is one, otherwise creates a camera.</summary>
        public static CameraRig Create(SimulationManager manager)
        {
            Camera view = Camera.main;
            if (view == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                view = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            view.orthographic = false;
            view.fieldOfView = 50f;
            view.nearClipPlane = 1f;
            view.farClipPlane = 600f;

            CameraRig rig = view.gameObject.GetComponent<CameraRig>();
            if (rig == null) rig = view.gameObject.AddComponent<CameraRig>();
            rig.sim = manager;
            rig.ShowOverview();
            return rig;
        }

        public void ToggleFollow()
        {
            FollowScooter = !FollowScooter;
            if (!FollowScooter) ShowOverview();
        }

        public void ShowOverview()
        {
            FollowScooter = false;
            transform.position = OverviewPosition;
            transform.rotation = Quaternion.LookRotation(OverviewTarget - OverviewPosition, Vector3.up);
        }

        private void LateUpdate()
        {
            if (!FollowScooter || sim == null) return;

            ScooterController scooter = sim.PrimaryScooter;
            if (scooter == null)
            {
                ShowOverview();
                return;
            }

            Vector3 focus = scooter.Position + Vector3.up * 1.2f;
            Vector3 wanted = scooter.Position - scooter.Forward * 11f + Vector3.up * 6.5f;

            // Snap when the scooter jumps to a new trip, glide otherwise.
            bool jumped = (transform.position - wanted).sqrMagnitude > 40f * 40f;
            float blend = jumped ? 1f : 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, wanted, blend);

            Quaternion look = Quaternion.LookRotation(focus + scooter.Forward * 4f - transform.position, Vector3.up);
            transform.rotation = jumped ? look : Quaternion.Slerp(transform.rotation, look, blend);
        }
    }
}
