using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class VehicleEnterExit : MonoBehaviour
    {
        public Transform Seat;
        public Transform ExitPoint;
        public VehicleController Vehicle;
        PlayerController occupant;

        public void Enter(PlayerController player, VehicleController vehicle)
        {
            if (player == null || vehicle == null || occupant != null) return;
            occupant = player;
            Vehicle = vehicle;
            player.BeginDriving(vehicle, Seat);
            vehicle.SetOccupied(true);
            player.NotifyInteracted(vehicle.VehicleId);
        }

        public void Exit()
        {
            if (occupant == null) return;
            var player = occupant;
            occupant = null;
            if (Vehicle != null) Vehicle.SetOccupied(false);
            var exit = ExitPoint != null ? ExitPoint.position : transform.position + transform.right * 2.2f;
            player.EndDriving(exit, transform.eulerAngles.y);
        }
    }
}
