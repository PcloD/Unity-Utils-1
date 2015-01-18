﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Utils.Event
{
	[ExecuteInEditMode]
    public class EventHandler : MonoBehaviour , IActionOwner , INodeOwner , ISerializationCallbackReceiver
    {
        #region Enum Definition
	
        /// <summary>
        /// State of the handler
        /// </summary>
        public enum HandlerState
        {
            NONE,
            RUNNING,
            SLEEPING,
            TERMINATED
        }

        #endregion

        #region Static Members

        public const int s_kindFixedCountMask = 0x01;
        public const int s_kindLockMask       = 0x02;

        #endregion 

        #region Private Members

		/// <summary>
		/// Action to trigger
		/// </summary>
		[HideInInspector]
		[UnityEngine.SerializeField]
		private GPAction m_action;

		/// <summary>
		/// Whether or not the handler can fire a fixed or infinite number of time
		/// its action.
		/// </summary>
		[UnityEngine.SerializeField]
		private bool m_usesFixedCount;

		/// <summary>
		/// Whether or not the handler can fire its action while it's already running.
		/// </summary>
		[UnityEngine.SerializeField]
		private bool m_usesLockUntilCompletion;

        /// <summary>
        /// Number of times the event has been triggered
        /// </summary>
        private int m_triggerCount;

        /// <summary>
        /// Current state of the handler
        /// </summary>
        private HandlerState m_currState = HandlerState.NONE;

        #endregion

        #region Public Members

        /// <summary>
        /// Name of the event the handler is listening
        /// </summary>
        public GPEventID _eventID;

        /// <summary>
        /// Maximum number of time the event can be triggered
        /// </summary>
        public int _maxTriggerCount;

#if UNITY_EDITOR

		[UnityEngine.HideInInspector]
		public ActionEditorNode _eventNode;

		[UnityEngine.HideInInspector]
		public Rect _windowRect = new Rect(0,0,100,50);

#endif

        #endregion

        #region Properties

		public GPAction Action
		{
			get{ return m_action;  }
			set
			{ 
#if UNITY_EDITOR
				Disconnect(m_action);
#endif
				m_action = value; 
#if UNITY_EDITOR
				Connect(m_action);
#endif
			}
		}

        /// <summary>
        /// Readonly access to the handler state.
        /// </summary>
        public HandlerState State
        {
            get { return m_currState; }
        }

		public GPEvent CurrentEvent
		{
			get; set;
		}

#if UNITY_EDITOR
		
		public Rect WindowRect
		{
			get{ return _windowRect; }
			set{ _windowRect = value; }
		}
		
#endif

        #endregion

		#region Constructors

		public EventHandler()
		{
		}

		#endregion

        #region ContextMenu

        [ContextMenu("Show GPActionObject")]
        private void ShowAllActions()
        {
            GameObject obj = GetGPActionObjectOrCreate();

            obj.hideFlags = HideFlags.None;
        }


        [ContextMenu("Hide GPActionObject")]
        private void HideAllActions()
        {
            GameObject obj = GetGPActionObjectOrCreate();

            obj.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
        }

        #endregion 

        #region MonoBehaviour

        void Start()
        {
			if(!Application.isPlaying)
				return;

            Init();
        }

        void Update()
        {
			if(!Application.isPlaying)
				return;

            if(Action == null)
                return;

            if(Action.HasEnded)
            {
				CurrentEvent = null;

            	if(HasReachedMaxTriggerCount())
             		m_currState = HandlerState.TERMINATED;
            	else
            		m_currState = HandlerState.SLEEPING;
			
				return;
            }

			if(Action.IsRunning)
				Action.Update();
        }

		void OnDrawGizmos()
		{
			if(Action != null)
				Action.OnDrawGizmos();
		}

		void OnDrawGizmosSelected()
		{
			if(Action != null)
				Action.OnDrawGizmosSelected();
		}

		void OnDestroy()
		{
			GPActionObjectMapper obj = GetGPActionObjectMapper();

			if(obj == null)
				return;

			obj.RemoveEventHandler(this);
		}

        #endregion

        #region Event Listening

        public void Init()
        {
            if(_eventID == GPEventID.Invalid)
                throw new System.Exception("Null event name");

            EventManager.Instance.Register(_eventID.ID, EventTrigger);

			if(Action != null)
				Action.SetParentHandler(this);
        }

        public void EventTrigger(GPEvent evt)
        {
            if (!evt.EventID.Equals(_eventID) || Action == null)
                return;

			CurrentEvent = evt;

            if(CanTriggerAction())
               TriggerAction();
        }

        #endregion

#if UNITY_EDITOR

		public void CreateEventNode()
		{
			_eventNode = new ActionEditorNode();

			_eventNode._owner = this;
			_eventNode._center = new Vector2(92,25);

			if(m_action != null)
				Connect(m_action);
			else
				_eventNode._connection = null;
		}

		#region IActionOwner

		public void Connect(GPAction action)
		{
			if(action == null)
				return;

			if(action._leftNode._connection != null)
			{
				((IActionOwner)action._leftNode._connection._nodeParent._owner).Disconnect(action);
			}

			_eventNode._connection = new ActionEditorConnection(_eventNode,action._leftNode);
			action._leftNode._connection = _eventNode._connection;
		}

		public void Disconnect(GPAction Action)
		{
			if(m_action == null)
				return;

			m_action._leftNode._connection = null;
			_eventNode._connection = null;
		}

		public void DisconnectAll()
		{
			Disconnect(m_action);
		}

		#endregion

#endif

		#region Serialization Callbacks

		public void OnBeforeSerialize(){}

		public void OnAfterDeserialize()
		{
			CreateEventNode();
		}

		#endregion

        #region Private Utils

        private bool CanTriggerAction()
        {
            if(HasReachedMaxTriggerCount())
                return false;
           
			if (m_usesLockUntilCompletion && Action.IsRunning)
                return false;

            return true;
        }

        private bool HasReachedMaxTriggerCount()
        {
        	// Check if handler kind has fixed count and 
			// if current count allows to run one more time.

            return (m_usesFixedCount && m_triggerCount >= _maxTriggerCount);
        }

        private void TriggerAction()
        {
            m_currState = HandlerState.RUNNING;
       
            m_triggerCount++;
            Action.Trigger();
        }

        #endregion

        #region GPActionObjectMapper Wrapping

        public virtual GameObject GetGPActionObject()
        {
            return GPActionUtils.GetGPActionObject(this.gameObject);
        }

        public virtual GPActionObjectMapper GetGPActionObjectMapper()
        {
            return GPActionUtils.GetGPActionObjectMapper(this.gameObject);
        }

        public virtual GameObject GetGPActionObjectOrCreate()
        {
            return GPActionUtils.GetGPActionObjectOrCreate(this.gameObject);
        }

        public virtual GPActionObjectMapper GetGPActionObjectMapperOrCreate()
        {
            return GPActionUtils.GetGPActionObjectMapperOrCreate(this.gameObject);
        }

        public virtual GPAction AddAction(System.Type actionType)
        {
            GPAction action = GetGPActionObjectMapperOrCreate().AddAction(this,actionType);

            action.enabled = false;

            action._name = System.Guid.NewGuid().ToString();

            action.SetParentHandler(this);

            return action;
        }

        #endregion

	
    }
}
