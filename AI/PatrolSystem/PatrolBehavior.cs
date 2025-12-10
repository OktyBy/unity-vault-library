using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace UnityVault.AI
{
    /// <summary>
    /// Waypoint-based patrol behavior for AI.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class PatrolBehavior : MonoBehaviour
    {
        [Header("Patrol Settings")]
        [SerializeField] private PatrolPath patrolPath;
        [SerializeField] private PatrolMode patrolMode = PatrolMode.Loop;
        [SerializeField] private float waypointReachDistance = 0.5f;
        [SerializeField] private float waitTimeAtWaypoint = 2f;

        [Header("Movement")]
        [SerializeField] private float patrolSpeed = 2f;
        [SerializeField] private bool lookAtWaypoint = true;
        [SerializeField] private float rotationSpeed = 5f;

        // Components
        private NavMeshAgent agent;

        // State
        private int currentWaypointIndex = 0;
        private bool isWaiting = false;
        private float waitTimer = 0f;
        private bool isReversing = false;
        private bool isPatrolling = true;

        // Properties
        public bool IsPatrolling => isPatrolling;
        public bool IsWaiting => isWaiting;
        public int CurrentWaypointIndex => currentWaypointIndex;
        public Vector3 CurrentWaypoint => patrolPath?.GetWaypoint(currentWaypointIndex) ?? transform.position;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.speed = patrolSpeed;
        }

        private void Start()
        {
            if (patrolPath != null && patrolPath.WaypointCount > 0)
            {
                GoToNextWaypoint();
            }
        }

        private void Update()
        {
            if (!isPatrolling || patrolPath == null || patrolPath.WaypointCount == 0) return;

            if (isWaiting)
            {
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0)
                {
                    isWaiting = false;
                    GoToNextWaypoint();
                }
            }
            else
            {
                CheckWaypointReached();
            }
        }

        private void CheckWaypointReached()
        {
            if (!agent.pathPending && agent.remainingDistance <= waypointReachDistance)
            {
                OnWaypointReached();
            }
        }

        private void OnWaypointReached()
        {
            var waypoint = patrolPath.GetWaypointData(currentWaypointIndex);

            if (waypoint != null && waypoint.waitTime > 0)
            {
                isWaiting = true;
                waitTimer = waypoint.waitTime;
            }
            else if (waitTimeAtWaypoint > 0)
            {
                isWaiting = true;
                waitTimer = waitTimeAtWaypoint;
            }
            else
            {
                GoToNextWaypoint();
            }
        }

        private void GoToNextWaypoint()
        {
            switch (patrolMode)
            {
                case PatrolMode.Loop:
                    currentWaypointIndex = (currentWaypointIndex + 1) % patrolPath.WaypointCount;
                    break;

                case PatrolMode.PingPong:
                    if (isReversing)
                    {
                        currentWaypointIndex--;
                        if (currentWaypointIndex < 0)
                        {
                            currentWaypointIndex = 1;
                            isReversing = false;
                        }
                    }
                    else
                    {
                        currentWaypointIndex++;
                        if (currentWaypointIndex >= patrolPath.WaypointCount)
                        {
                            currentWaypointIndex = patrolPath.WaypointCount - 2;
                            isReversing = true;
                        }
                    }
                    break;

                case PatrolMode.Random:
                    currentWaypointIndex = Random.Range(0, patrolPath.WaypointCount);
                    break;

                case PatrolMode.Once:
                    currentWaypointIndex++;
                    if (currentWaypointIndex >= patrolPath.WaypointCount)
                    {
                        isPatrolling = false;
                        return;
                    }
                    break;
            }

            agent.SetDestination(patrolPath.GetWaypoint(currentWaypointIndex));
        }

        public void StartPatrol()
        {
            isPatrolling = true;
            GoToNextWaypoint();
        }

        public void StopPatrol()
        {
            isPatrolling = false;
            agent.ResetPath();
        }

        public void SetPatrolPath(PatrolPath path)
        {
            patrolPath = path;
            currentWaypointIndex = 0;
            isReversing = false;

            if (isPatrolling)
            {
                GoToNextWaypoint();
            }
        }
    }

    public enum PatrolMode
    {
        Loop,
        PingPong,
        Random,
        Once
    }

    /// <summary>
    /// Patrol path containing waypoints.
    /// </summary>
    public class PatrolPath : MonoBehaviour
    {
        [SerializeField] private List<Waypoint> waypoints = new List<Waypoint>();
        [SerializeField] private Color pathColor = Color.cyan;
        [SerializeField] private bool drawPath = true;

        public int WaypointCount => waypoints.Count;

        public Vector3 GetWaypoint(int index)
        {
            if (index < 0 || index >= waypoints.Count) return transform.position;
            return waypoints[index].transform.position;
        }

        public Waypoint GetWaypointData(int index)
        {
            if (index < 0 || index >= waypoints.Count) return null;
            return waypoints[index];
        }

        private void OnDrawGizmos()
        {
            if (!drawPath || waypoints.Count < 2) return;

            Gizmos.color = pathColor;

            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                if (waypoints[i] != null && waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(waypoints[i].transform.position, waypoints[i + 1].transform.position);
                }
            }

            // Draw closing line for loop
            if (waypoints[0] != null && waypoints[waypoints.Count - 1] != null)
            {
                Gizmos.color = new Color(pathColor.r, pathColor.g, pathColor.b, 0.3f);
                Gizmos.DrawLine(waypoints[waypoints.Count - 1].transform.position, waypoints[0].transform.position);
            }
        }
    }

    /// <summary>
    /// Single waypoint in a patrol path.
    /// </summary>
    public class Waypoint : MonoBehaviour
    {
        public float waitTime = 0f;
        public bool lookAtNext = true;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}
