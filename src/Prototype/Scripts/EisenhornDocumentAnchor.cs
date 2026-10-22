using System;
using Kingmaker;
using UnityEngine;
namespace ArmouryEisenhorn.Prototype
{
    // A scene-authored native map object, never a runtime-spawned object.
    // For the proof of concept, place it at the protagonist's feet on each scene
    // load. This avoids guessed world coordinates and keeps retroactive saves usable.
    public sealed class EisenhornDocumentAnchor : MonoBehaviour
    {
        private bool positioned;
        private float nextAttempt;
        private bool reportedFailure;
        private void Update()
        {
            if (positioned || Time.unscaledTime < nextAttempt || !PrototypeRuntime.IsSafeWorld()) return;
            nextAttempt = Time.unscaledTime + 1f;
            try
            {
                var view = GetComponent<EisenhornDocumentView>();
                var unit = Game.Instance.Player.MainCharacterEntity;
                if (view?.Data == null || unit == null) return;
                // Same traversable ground location as the player; the visual is a
                // raised paper marker and the trigger collider never blocks movement.
                var position = unit.Position;
                view.Data.Position = position;
                transform.position = position;
                positioned = true;
                PrototypeRuntime.Log("Native document ready in " + Game.Instance.CurrentlyLoadedArea.name
                    + " at protagonist position " + position);
            }
            catch (Exception ex)
            {
                if (reportedFailure) return;
                reportedFailure = true;
                PrototypeRuntime.Log("ERROR positioning native document: " + ex);
            }
        }
    }
}
