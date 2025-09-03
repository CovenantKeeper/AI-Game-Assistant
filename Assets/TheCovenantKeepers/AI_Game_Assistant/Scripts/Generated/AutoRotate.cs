using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    public class AutoRotate : MonoBehaviour
    {
        [SerializeField, Tooltip("Rotation speed in degrees per second.")]
        private float rotationSpeed = 45f;

        void Update()
        {
            if (Time.timeScale > 0)
            {
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            }
        }
    }
}