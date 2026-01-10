using UnityEngine;
using HoaxGames;

public class FootIKLocalPhysics : FootIK
{
    private ScenePhysics _scenePhysics;

    protected override void Awake()
    {
        base.Awake();
        _scenePhysics = GetComponent<ScenePhysics>();
        
        if (_scenePhysics == null)
        {
            Debug.LogError($"FootIKLocalPhysics on {gameObject.name} requires ScenePhysics component!", this);
        }
    }

    protected override void isValidAndGrounded(GroundedResult groundedResult, Vector3 feetZeroPos, Vector3 upVec, int collisionLayer)
    {
        if (m_animator == null)
        {
            groundedResult.init(false, false, null, Vector3.zero, Vector3.zero, Vector3.zero);
            return;
        }
        if (m_animator.enabled == false)
        {
            groundedResult.init(false, false, null, Vector3.zero, Vector3.zero, Vector3.zero);
            return;
        }

        bool isValid = true;

        if (m_validationType == ValidationType.FORCE_INVALID)
        {
            isValid = false;
        }
        else if (m_validationType == ValidationType.FORCE_VALID)
        {
            groundedResult.init(true, true, m_transform, feetZeroPos, feetZeroPos, upVec);
            m_lastPotentiallyValidGroundedResult.init(true, true, m_transform, feetZeroPos, feetZeroPos, upVec);
            return;
        }

        foreach (var entry in m_deactiveOnAnimatorState)
        {
            if (m_animator.layerCount > entry.Key)
            {
                var nextAnimState = m_animator.GetNextAnimatorStateInfo(entry.Key);
                if (nextAnimState.shortNameHash == entry.Value && nextAnimState.normalizedTime > 0) isValid = false;

                bool hasStartedTransitionToAnotherState = nextAnimState.shortNameHash != entry.Value && nextAnimState.normalizedTime > 0;
                if (hasStartedTransitionToAnotherState == false && m_animator.GetCurrentAnimatorStateInfo(entry.Key).shortNameHash == entry.Value) isValid = false;
            }
        }

        if (m_autoUngroundUpwardsVelocity > 0)
        {
            Vector3 distVec = Vector3.Project(feetZeroPos - (m_prevTransformPosition + m_characterExternalMovingPlatformOffsetVec), upVec);

            float dot = Vector3.Dot(distVec.normalized, upVec);
            if (dot > 0.975f)
            {
                m_upwardsVelocity = 0;
                if (Time.deltaTime > 0) m_upwardsVelocity = distVec.magnitude / Time.deltaTime;
                m_maxUpwardsVelocity = Mathf.Max(m_upwardsVelocity, m_maxUpwardsVelocity);

                if (m_upwardsVelocity > m_autoUngroundUpwardsVelocity)
                {
                    groundedResult.init(isValid, false, null, Vector3.zero, Vector3.zero, Vector3.zero);
                    return;
                }
            }
        }

        float raycastUpwardsOffset = m_checkGroundedRadius + m_checkGroundedDistance;
        float raycastDistance = raycastUpwardsOffset + m_checkGroundedDistance - m_checkGroundedRadius;

        RaycastHit hit;
        Vector3 origin = feetZeroPos + upVec * raycastUpwardsOffset;
        
        if (_scenePhysics.SphereCast(origin, m_checkGroundedRadius, -upVec, out hit, raycastDistance, collisionLayer, m_triggerCollisionInteraction))
        {
            groundedResult.init(isValid, true, hit.transform, hit.point, origin - upVec * (hit.distance + m_checkGroundedRadius), hit.normal);
            m_lastPotentiallyValidGroundedResult.init(isValid, true, hit.transform, hit.point, groundedResult.groundedPosition, hit.normal);
            return;
        }

        groundedResult.init(isValid, false, null, Vector3.zero, Vector3.zero, Vector3.zero);
    }

    protected override void findNewIKPos(IKResult ikResult, Vector3 ikPos, Vector3 ikBottomPoint, float heightOffset, float forwardBias, Vector3 forwardDir, Quaternion rotation, Vector3 origin, Vector3 rayDirection, float maxIKCorrection, float ikCorrectionRaycastMargin, float maxFootCorrection, float maxCenterOfMassCorrection, float raycastLength, float raycastWidth, int raycastLayer)
    {
        float boxCastHeight = Mathf.Max(heightOffset * 0.01f, 0.01f);

        Vector3 rayOriginPoint = ikBottomPoint - rayDirection * (maxIKCorrection + boxCastHeight + ikCorrectionRaycastMargin);
        Vector3 rayOrigin = rayOriginPoint + forwardBias * forwardDir;

        Ray ray = new Ray(rayOrigin, rayDirection);
        RaycastHit hit;
        float raycastDistance = maxIKCorrection + maxCenterOfMassCorrection + ikCorrectionRaycastMargin;

        if (_scenePhysics.BoxCast(ray.origin, new Vector3(raycastWidth * 0.5f, boxCastHeight, raycastLength * 0.5f), ray.direction, out hit, rotation, raycastDistance, raycastLayer, m_triggerCollisionInteraction))
        {
            Vector3 hitPoint = rayOrigin + hit.distance * rayDirection + boxCastHeight * rayDirection;
            Vector3 foundBottomPoint = hitPoint - forwardDir * forwardBias;
            Vector3 originalFoundIKPoint = foundBottomPoint - rayDirection * heightOffset;

            if (float.IsNaN(hit.normal.x) || float.IsNaN(hit.normal.y) || float.IsNaN(hit.normal.z))
            {
                hit.normal = -rayDirection;
#if UNITY_EDITOR
                Debug.LogError($"iStep detected a collider with broken size [GameObject: {hit.collider.name}].");
#endif
            }

            Vector3 normalToUse = hit.normal;
            float alpha = Vector3.Angle(-rayDirection, normalToUse);

            RaycastHit fixHit;
            float radius = heightOffset;
            Vector3 fixOrigin = rayOriginPoint - radius * rayDirection;
            Vector3? ikPointFixHit = null;
            
            if (_scenePhysics.SphereCast(fixOrigin, radius, rayDirection, out fixHit, raycastDistance, raycastLayer, m_triggerCollisionInteraction))
            {
                ikPointFixHit = fixOrigin + fixHit.distance * rayDirection;

                float beta = Vector3.Angle(-rayDirection, fixHit.normal);

                if (Mathf.Abs(beta - alpha) > 1.0f)
                {
                    Vector3 hitpointsVec = fixHit.point - hit.point;
                    if (hitpointsVec.magnitude > 0.001f)
                    {
                        hitpointsVec.Normalize();
                        Vector3 r = Vector3.Cross(hitpointsVec, -rayDirection).normalized;
                        Vector3 fixNormal = Vector3.Cross(r, hitpointsVec).normalized;
                        float fixAlpha = Vector3.Angle(-rayDirection, fixNormal);

                        if (Mathf.Abs(fixAlpha) < Mathf.Abs(alpha))
                        {
                            normalToUse = fixNormal;
                            alpha = fixAlpha;

                            if (Mathf.Abs(beta) > Mathf.Abs(alpha))
                            {
                                alpha = beta;
                                normalToUse = fixHit.normal;
                            }
                        }
                    }
                }
            }

            if (Mathf.Abs(alpha) > m_maxGroundAngleAtWhichTheGroundIsDetectedAsGround)
            {
                ikResult.init(ikPos, ikPos, false, ikBottomPoint, ikBottomPoint, -rayDirection, null, null);
                return;
            }

            alpha = Mathf.Clamp(alpha, -m_maxGroundAngleTheFeetsCanAdaptTo, m_maxGroundAngleTheFeetsCanAdaptTo);

            Vector3 pointsDistVec = foundBottomPoint - hit.point;
            float l = pointsDistVec.magnitude;
            Vector3 c1 = Vector3.Cross(pointsDistVec.normalized, normalToUse).normalized;
            Vector3 c2 = Vector3.Cross(normalToUse, c1).normalized;
            float cosAlpha = Mathf.Cos(alpha * Mathf.Deg2Rad);

            if (cosAlpha > 0 && cosAlpha < 0.01f) cosAlpha = 0.01f;
            else if (cosAlpha < 0 && cosAlpha > -0.01f) cosAlpha = -0.01f;

            float ll = l / cosAlpha;
            Vector3 angleOffset = Vector3.Project(c2 * ll, -rayDirection);
            float realHeightOffset = heightOffset / cosAlpha;

            foundBottomPoint = foundBottomPoint + angleOffset;
            Vector3 foundIKPos = foundBottomPoint - rayDirection * realHeightOffset;
            Vector3 surfacePoint = foundIKPos + rayDirection * heightOffset;

            Vector3 movementDirVec = foundIKPos - originalFoundIKPoint;
            if (ikPointFixHit.HasValue && Vector3.Dot(movementDirVec.normalized, -rayDirection) > 0)
            {
                Vector3 ikPointsDistVec = ikPointFixHit.Value - foundIKPos;

                if (Vector3.Dot(ikPointsDistVec.normalized, rayDirection) > 0)
                {
                    foundIKPos += ikPointsDistVec;
                    surfacePoint += ikPointsDistVec;
                    foundBottomPoint += ikPointsDistVec;
                }
            }

            bool isGlued = true;
            Vector3 gluedIkPos = foundIKPos;
            Vector3 distVec = foundIKPos - ikPos;
            if (Vector3.Dot(distVec.normalized, rayDirection) > 0)
            {
                foundIKPos = ikPos;
                isGlued = false;

                if (distVec.magnitude > maxFootCorrection)
                {
                    Vector3 surfaceOffsetVec = -rayDirection * (distVec.magnitude - maxFootCorrection);
                    surfacePoint = surfacePoint + surfaceOffsetVec;
                    foundBottomPoint = foundBottomPoint + surfaceOffsetVec;
                }
            }
            else
            {
                distVec = foundIKPos - ikBottomPoint;
                if (distVec.magnitude > (maxIKCorrection + heightOffset))
                {
                    foundIKPos = ikBottomPoint + distVec.normalized * (maxIKCorrection + heightOffset);
                    gluedIkPos = foundIKPos;
                }
            }

            ikResult.init(foundIKPos, gluedIkPos, isGlued, foundBottomPoint, surfacePoint, normalToUse, hit.transform, fixHit.transform);
            return;
        }

        ikResult.init(ikPos, ikPos, false, ikBottomPoint, ikBottomPoint, -rayDirection, null, null);
    }
}
