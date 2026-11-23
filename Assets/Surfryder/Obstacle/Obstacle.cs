using System.Collections.Generic;
using UnityEngine;

public class Obstacle : MonoBehaviour
{
    [Header("Observerable Variables (Don't edit!)")]
    [SerializeField] float forwardVelocity;
    [SerializeField] float sidewaysVelocity;

    // Every live obstacle, so the sun can check whether one of them blocks its light.
    public static readonly List<Obstacle> Active = new List<Obstacle>();

    Renderer[] renderers;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();   // the visible building (inactive helper meshes are skipped)
    }

    void OnEnable()  { Active.Add(this); }
    void OnDisable() { Active.Remove(this); }

    // True if the ray hits this building's visible mesh bounds within maxDistance.
    // Matches the shadow the building casts, unlike the smaller gameplay trigger collider.
    public bool BlocksRay(Ray ray, float maxDistance)
    {
        foreach (Renderer r in renderers)
        {
            if (r != null && r.enabled && r.bounds.IntersectRay(ray, out float distance) && distance <= maxDistance)
                return true;
        }
        return false;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (GameManager.gameManager.gameOver) return;

        forwardVelocity = GameManager.gameManager.forwardVelocity;
        sidewaysVelocity = GameManager.gameManager.sidewaysVelocity;

        Vector3 movement = new Vector3(
            -sidewaysVelocity,
            0f,
            -forwardVelocity
        );

        transform.Translate(movement * Time.fixedDeltaTime, Space.World);

        if (transform.position.z < -20.0f) { Destroy(gameObject); }
    }

    void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            Debug.Log("Collision");
            GameManager.gameManager.GameOver();
        }
    }
}
