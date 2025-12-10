using UnityEngine;
using System;
using System.Collections.Generic;

namespace UnityVault.AI
{
    /// <summary>
    /// Behavior Tree system for complex AI decision making.
    /// </summary>
    public class BehaviorTree : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float tickRate = 0.1f;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool debugMode = false;

        // Root node
        private BTNode rootNode;
        private float lastTickTime;
        private bool isRunning;

        // Blackboard for shared data
        private Blackboard blackboard = new Blackboard();

        // Properties
        public bool IsRunning => isRunning;
        public Blackboard Data => blackboard;
        public BTNode RootNode => rootNode;

        // Events
        public event Action<BTNode, NodeState> NodeStateChanged;
        public event Action TreeStarted;
        public event Action TreeStopped;

        private void Start()
        {
            if (autoStart && rootNode != null)
            {
                StartTree();
            }
        }

        private void Update()
        {
            if (!isRunning || rootNode == null) return;

            if (Time.time - lastTickTime >= tickRate)
            {
                lastTickTime = Time.time;
                Tick();
            }
        }

        public void SetRootNode(BTNode node)
        {
            rootNode = node;
            if (node != null)
            {
                node.SetTree(this);
            }
        }

        public void StartTree()
        {
            if (rootNode == null)
            {
                Debug.LogWarning("[BehaviorTree] No root node set!");
                return;
            }

            isRunning = true;
            TreeStarted?.Invoke();
            Debug.Log("[BehaviorTree] Started");
        }

        public void StopTree()
        {
            isRunning = false;
            rootNode?.Reset();
            TreeStopped?.Invoke();
            Debug.Log("[BehaviorTree] Stopped");
        }

        public void Tick()
        {
            if (rootNode == null) return;

            var state = rootNode.Evaluate();

            if (debugMode)
            {
                Debug.Log($"[BehaviorTree] Tick result: {state}");
            }
        }

        internal void OnNodeStateChanged(BTNode node, NodeState state)
        {
            NodeStateChanged?.Invoke(node, state);
        }
    }

    /// <summary>
    /// Blackboard for storing shared AI data.
    /// </summary>
    public class Blackboard
    {
        private Dictionary<string, object> data = new Dictionary<string, object>();

        public void Set<T>(string key, T value)
        {
            data[key] = value;
        }

        public T Get<T>(string key, T defaultValue = default)
        {
            if (data.TryGetValue(key, out object value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        public bool Has(string key) => data.ContainsKey(key);
        public void Remove(string key) => data.Remove(key);
        public void Clear() => data.Clear();
    }

    public enum NodeState
    {
        Running,
        Success,
        Failure
    }

    /// <summary>
    /// Base class for all behavior tree nodes.
    /// </summary>
    public abstract class BTNode
    {
        protected BehaviorTree tree;
        protected NodeState state = NodeState.Running;
        public string Name { get; set; }

        public NodeState State => state;

        public void SetTree(BehaviorTree bt)
        {
            tree = bt;
            OnSetTree();
        }

        protected virtual void OnSetTree() { }

        public abstract NodeState Evaluate();

        public virtual void Reset()
        {
            state = NodeState.Running;
        }

        protected void SetState(NodeState newState)
        {
            state = newState;
            tree?.OnNodeStateChanged(this, state);
        }
    }

    /// <summary>
    /// Composite node with children.
    /// </summary>
    public abstract class BTComposite : BTNode
    {
        protected List<BTNode> children = new List<BTNode>();

        public void AddChild(BTNode node)
        {
            children.Add(node);
            if (tree != null)
            {
                node.SetTree(tree);
            }
        }

        protected override void OnSetTree()
        {
            foreach (var child in children)
            {
                child.SetTree(tree);
            }
        }

        public override void Reset()
        {
            base.Reset();
            foreach (var child in children)
            {
                child.Reset();
            }
        }
    }

    /// <summary>
    /// Sequence node - runs children in order until one fails.
    /// </summary>
    public class BTSequence : BTComposite
    {
        private int currentChild = 0;

        public override NodeState Evaluate()
        {
            while (currentChild < children.Count)
            {
                var childState = children[currentChild].Evaluate();

                switch (childState)
                {
                    case NodeState.Running:
                        SetState(NodeState.Running);
                        return state;
                    case NodeState.Failure:
                        currentChild = 0;
                        SetState(NodeState.Failure);
                        return state;
                    case NodeState.Success:
                        currentChild++;
                        break;
                }
            }

            currentChild = 0;
            SetState(NodeState.Success);
            return state;
        }

        public override void Reset()
        {
            base.Reset();
            currentChild = 0;
        }
    }

    /// <summary>
    /// Selector node - runs children until one succeeds.
    /// </summary>
    public class BTSelector : BTComposite
    {
        private int currentChild = 0;

        public override NodeState Evaluate()
        {
            while (currentChild < children.Count)
            {
                var childState = children[currentChild].Evaluate();

                switch (childState)
                {
                    case NodeState.Running:
                        SetState(NodeState.Running);
                        return state;
                    case NodeState.Success:
                        currentChild = 0;
                        SetState(NodeState.Success);
                        return state;
                    case NodeState.Failure:
                        currentChild++;
                        break;
                }
            }

            currentChild = 0;
            SetState(NodeState.Failure);
            return state;
        }

        public override void Reset()
        {
            base.Reset();
            currentChild = 0;
        }
    }

    /// <summary>
    /// Parallel node - runs all children simultaneously.
    /// </summary>
    public class BTParallel : BTComposite
    {
        public enum Policy { RequireOne, RequireAll }

        private Policy successPolicy = Policy.RequireOne;
        private Policy failurePolicy = Policy.RequireAll;

        public BTParallel(Policy success = Policy.RequireOne, Policy failure = Policy.RequireAll)
        {
            successPolicy = success;
            failurePolicy = failure;
        }

        public override NodeState Evaluate()
        {
            int successCount = 0;
            int failureCount = 0;

            foreach (var child in children)
            {
                var childState = child.Evaluate();

                switch (childState)
                {
                    case NodeState.Success:
                        successCount++;
                        break;
                    case NodeState.Failure:
                        failureCount++;
                        break;
                }
            }

            if (successPolicy == Policy.RequireOne && successCount > 0)
            {
                SetState(NodeState.Success);
                return state;
            }

            if (successPolicy == Policy.RequireAll && successCount == children.Count)
            {
                SetState(NodeState.Success);
                return state;
            }

            if (failurePolicy == Policy.RequireOne && failureCount > 0)
            {
                SetState(NodeState.Failure);
                return state;
            }

            if (failurePolicy == Policy.RequireAll && failureCount == children.Count)
            {
                SetState(NodeState.Failure);
                return state;
            }

            SetState(NodeState.Running);
            return state;
        }
    }

    /// <summary>
    /// Decorator node with single child.
    /// </summary>
    public abstract class BTDecorator : BTNode
    {
        protected BTNode child;

        public void SetChild(BTNode node)
        {
            child = node;
            if (tree != null)
            {
                child.SetTree(tree);
            }
        }

        protected override void OnSetTree()
        {
            child?.SetTree(tree);
        }

        public override void Reset()
        {
            base.Reset();
            child?.Reset();
        }
    }

    /// <summary>
    /// Inverter decorator - inverts child result.
    /// </summary>
    public class BTInverter : BTDecorator
    {
        public override NodeState Evaluate()
        {
            if (child == null)
            {
                SetState(NodeState.Failure);
                return state;
            }

            var childState = child.Evaluate();

            switch (childState)
            {
                case NodeState.Success:
                    SetState(NodeState.Failure);
                    break;
                case NodeState.Failure:
                    SetState(NodeState.Success);
                    break;
                default:
                    SetState(NodeState.Running);
                    break;
            }

            return state;
        }
    }

    /// <summary>
    /// Repeater decorator - repeats child N times or forever.
    /// </summary>
    public class BTRepeater : BTDecorator
    {
        private int repeatCount;
        private int currentCount;
        private bool repeatForever;

        public BTRepeater(int count = -1)
        {
            repeatForever = count < 0;
            repeatCount = count;
        }

        public override NodeState Evaluate()
        {
            if (child == null)
            {
                SetState(NodeState.Failure);
                return state;
            }

            var childState = child.Evaluate();

            if (childState == NodeState.Running)
            {
                SetState(NodeState.Running);
                return state;
            }

            currentCount++;
            child.Reset();

            if (!repeatForever && currentCount >= repeatCount)
            {
                SetState(NodeState.Success);
                return state;
            }

            SetState(NodeState.Running);
            return state;
        }

        public override void Reset()
        {
            base.Reset();
            currentCount = 0;
        }
    }

    /// <summary>
    /// Condition node - checks a condition.
    /// </summary>
    public class BTCondition : BTNode
    {
        private Func<bool> condition;

        public BTCondition(Func<bool> condition)
        {
            this.condition = condition;
        }

        public override NodeState Evaluate()
        {
            SetState(condition != null && condition() ? NodeState.Success : NodeState.Failure);
            return state;
        }
    }

    /// <summary>
    /// Action node - performs an action.
    /// </summary>
    public class BTAction : BTNode
    {
        private Func<NodeState> action;

        public BTAction(Func<NodeState> action)
        {
            this.action = action;
        }

        public override NodeState Evaluate()
        {
            if (action == null)
            {
                SetState(NodeState.Failure);
                return state;
            }

            SetState(action());
            return state;
        }
    }

    /// <summary>
    /// Wait node - waits for specified duration.
    /// </summary>
    public class BTWait : BTNode
    {
        private float duration;
        private float startTime;
        private bool started;

        public BTWait(float seconds)
        {
            duration = seconds;
        }

        public override NodeState Evaluate()
        {
            if (!started)
            {
                started = true;
                startTime = Time.time;
            }

            if (Time.time - startTime >= duration)
            {
                SetState(NodeState.Success);
            }
            else
            {
                SetState(NodeState.Running);
            }

            return state;
        }

        public override void Reset()
        {
            base.Reset();
            started = false;
        }
    }
}
