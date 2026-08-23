using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Verteilt die vorhandenen Markt-Routen bei jedem Laden der Szene neu auf die NPCs.
/// Die Zuordnung ist immer ein Tausch: Kein NPC behaelt dabei seine Standardroute.
/// </summary>
[DisallowMultipleComponent]
public class FarmMarketNpcRouteRandomizer : MonoBehaviour
{
    [SerializeField] private bool randomizeDirection = true;

    private void Start()
    {
        FarmMarketNpcPatrol[] childPatrols = GetComponentsInChildren<FarmMarketNpcPatrol>(true);
        var patrols = new List<FarmMarketNpcPatrol>(childPatrols.Length);
        var routes = new List<Vector3[]>(childPatrols.Length);

        foreach (FarmMarketNpcPatrol patrol in childPatrols)
        {
            Vector3[] route = patrol.GetRouteWorldPoints();
            if (route.Length < 2)
                continue;

            patrols.Add(patrol);
            routes.Add(route);
        }

        if (patrols.Count < 2)
            return;

        int[] routeAssignment = CreateDerangement(patrols.Count);
        for (int npcIndex = 0; npcIndex < patrols.Count; npcIndex++)
        {
            bool reverseRoute = randomizeDirection && Random.value < 0.5f;
            patrols[npcIndex].SetRuntimeRoute(routes[routeAssignment[npcIndex]], reverseRoute);
        }
    }

    private static int[] CreateDerangement(int count)
    {
        var assignment = new int[count];

        // Bei vier Markt-NPCs findet das normalerweise sofort eine komplett neue
        // Verteilung. Die Wiederholungsgrenze verhindert trotzdem eine Endlosschleife.
        for (int attempt = 0; attempt < 16; attempt++)
        {
            for (int i = 0; i < count; i++)
                assignment[i] = i;

            for (int i = count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                (assignment[i], assignment[swapIndex]) = (assignment[swapIndex], assignment[i]);
            }

            if (HasNoFixedPoints(assignment))
                return assignment;
        }

        // Garantierter Fallback: eine zyklische Verschiebung ist fuer count >= 2
        // immer eine gueltige Verteilung ohne die jeweilige Standardroute.
        for (int i = 0; i < count; i++)
            assignment[i] = (i + 1) % count;

        return assignment;
    }

    private static bool HasNoFixedPoints(int[] assignment)
    {
        for (int i = 0; i < assignment.Length; i++)
        {
            if (assignment[i] == i)
                return false;
        }

        return true;
    }
}
