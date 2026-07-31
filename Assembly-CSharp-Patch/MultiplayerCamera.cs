using System;
using UnityEngine;
using UnityEngine.Serialization;

[ExecutionDependency(typeof(PlayerControls))]
public class MultiplayerCamera : MonoBehaviour
{
	[NonSerialized]
	public Transform[] m_avatars;

	[SerializeField]
	public float m_gradientLimit = 0.5f;

	[SerializeField]
	public float m_timeToMax = 0.5f;

	[SerializeField]
	[FormerlySerializedAs("m_xEdgeBuffer")]
	[Range(0f, 0.5f)]
	public float m_leftEdgeBuffer = 0.2f;

	[SerializeField]
	[FormerlySerializedAs("m_xEdgeBuffer")]
	[Range(0f, 0.5f)]
	public float m_rightEdgeBuffer = 0.2f;

	[SerializeField]
	[FormerlySerializedAs("m_yEdgeBuffer")]
	[Range(0f, 0.5f)]
	public float m_topEdgeBuffer = 0.2f;

	[SerializeField]
	[FormerlySerializedAs("m_yEdgeBuffer")]
	[Range(0f, 0.5f)]
	public float m_bottomEdgeBuffer = 0.2f;

	[SerializeField]
	public float m_minDistance = 5f;

	[SerializeField]
	public float m_maxDistance = 50f;

	[SerializeField]
	public float m_maxUDistance = 2f;

	[SerializeField]
	public float m_maxRDistance = 2f;

	[NonSerialized]
	public Vector3 m_basePos;

	[NonSerialized]
	public float m_currentGradient;

	[NonSerialized]
	public Camera m_camera;

	[NonSerialized]
	public bool m_bStarted;

	public void Awake()
	{
		m_camera = base.gameObject.RequireComponentRecursive<Camera>();
		PlayerIDProvider.OnPlayerIDProviderDestroyed = (GenericVoid)Delegate.Combine(PlayerIDProvider.OnPlayerIDProviderDestroyed, new GenericVoid(OnPlayerIDProviderDestroyed));
		SetupAvatars();
		m_basePos = base.transform.position + base.transform.forward * GetAverageDistance();
		base.transform.position = GetIdealLocation();
		m_bStarted = true;
	}

	public void OnDestroy()
	{
		PlayerIDProvider.OnPlayerIDProviderDestroyed = (GenericVoid)Delegate.Remove(PlayerIDProvider.OnPlayerIDProviderDestroyed, new GenericVoid(OnPlayerIDProviderDestroyed));
	}

	public void OnPlayerIDProviderDestroyed()
	{
		if (m_bStarted)
		{
			SetupAvatars();
		}
	}

	public void SetupAvatars()
	{
		int count = PlayerIDProvider.s_AllProviders.Count;
		m_avatars = new Transform[count];
		for (int i = 0; i < count; i++)
		{
			m_avatars[i] = PlayerIDProvider.s_AllProviders._items[i].transform;
		}
	}

	private int GetValidAvatarCount()
	{
		if (m_avatars == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < m_avatars.Length; i++)
		{
			if (m_avatars[i] != null)
			{
				num++;
			}
		}
		return num;
	}

	private bool HasValidAvatars()
	{
		return GetValidAvatarCount() > 0;
	}

	private static bool IsFinite(Vector3 _value)
	{
		return !float.IsNaN(_value.x) && !float.IsNaN(_value.y) && !float.IsNaN(_value.z) && !float.IsInfinity(_value.x) && !float.IsInfinity(_value.y) && !float.IsInfinity(_value.z);
	}

	public float GetAverageDistance()
	{
		Camera camera = m_camera;
		Vector3 position = base.transform.position;
		Vector3 forward = base.transform.forward;
		float num = 0f;
		int validAvatarCount = GetValidAvatarCount();
		if (validAvatarCount > 0)
		{
			for (int i = 0; i < m_avatars.Length; i++)
			{
				if (m_avatars[i] == null)
				{
					continue;
				}
				Vector3 lhs = m_avatars[i].position - position;
				num += Vector3.Dot(lhs, forward);
			}
			return num / (float)validAvatarCount;
		}
		return m_minDistance;
	}

	public Vector3 GetCentralizedCameraPosition()
	{
		int validAvatarCount = GetValidAvatarCount();
		if (validAvatarCount > 0)
		{
			Camera camera = m_camera;
			Vector3 up = base.transform.up;
			float num = validAvatarCount;
			float num2 = 0f;
			for (int i = 0; i < m_avatars.Length; i++)
			{
				if (m_avatars[i] == null)
				{
					continue;
				}
				num2 += Vector3.Dot(m_avatars[i].position - m_basePos, up);
			}
			float value = 1f / num * num2;
			Vector3 right = base.transform.right;
			float num3 = 0f;
			for (int j = 0; j < m_avatars.Length; j++)
			{
				if (m_avatars[j] == null)
				{
					continue;
				}
				num3 += Vector3.Dot(m_avatars[j].position - m_basePos, right);
			}
			float value2 = 1f / num * num3;
			value = Mathf.Clamp(value, 0f - m_maxUDistance, m_maxUDistance);
			value2 = Mathf.Clamp(value2, 0f - m_maxRDistance, m_maxRDistance);
			return m_basePos + up * value + right * value2;
		}
		return m_basePos;
	}

	public float GetIdealDistance(Vector3 _centralisedCamera)
	{
		if (HasValidAvatars())
		{
			Camera camera = m_camera;
			float fieldOfView = camera.fieldOfView;
			float aspect = camera.aspect;
			Vector3 right = base.transform.right;
			Vector3 up = base.transform.up;
			Vector3 forward = base.transform.forward;
			float num = 0f;
			float num2 = Mathf.Tan(0.5f * fieldOfView);
			for (int i = 0; i < m_avatars.Length; i++)
			{
				if (m_avatars[i] == null)
				{
					continue;
				}
				Vector3 position = m_avatars[i].position;
				Vector3 lhs = position - _centralisedCamera;
				float num3 = Vector3.Dot(lhs, right) / (2f * aspect * num2 * (0.5f - m_rightEdgeBuffer));
				float num4 = (0f - Vector3.Dot(lhs, right)) / (2f * aspect * num2 * (0.5f - m_leftEdgeBuffer));
				float num5 = Vector3.Dot(lhs, up) / (2f * num2 * (0.5f - m_topEdgeBuffer));
				float num6 = (0f - Vector3.Dot(lhs, up)) / (2f * num2 * (0.5f - m_bottomEdgeBuffer));
				float num7 = Vector3.Dot(m_basePos - position, forward);
				num3 += num7;
				num4 += num7;
				num5 += num7;
				num6 += num7;
				num = Mathf.Max(num3, num);
				num = Mathf.Max(num4, num);
				num = Mathf.Max(num5, num);
				num = Mathf.Max(num6, num);
			}
			return num;
		}
		return m_minDistance;
	}

	public Vector3 GetIdealLocation()
	{
		Vector3 centralizedCameraPosition = GetCentralizedCameraPosition();
		float num = Mathf.Clamp(GetIdealDistance(centralizedCameraPosition), m_minDistance, m_maxDistance);
		return centralizedCameraPosition - num * base.transform.forward;
	}

	public void FixedUpdate()
	{
		if (m_bStarted)
		{
			if (!HasValidAvatars())
			{
				SetupAvatars();
			}
			Vector3 idealLocation = GetIdealLocation();
			if (!IsFinite(idealLocation) || !IsFinite(base.transform.position))
			{
				return;
			}
			float _nCurrentX = (idealLocation - base.transform.position).magnitude;
			float fixedDeltaTime = TimeManager.GetFixedDeltaTime(base.gameObject);
			MathUtils.AdvanceToTarget_Sinusoidal(ref _nCurrentX, ref m_currentGradient, 0f, m_gradientLimit, m_timeToMax, fixedDeltaTime);
			Vector3 position = idealLocation - (idealLocation - base.transform.position).SafeNormalised(Vector3.zero) * _nCurrentX;
			if (IsFinite(position))
			{
				base.transform.position = position;
			}
		}
	}
}
