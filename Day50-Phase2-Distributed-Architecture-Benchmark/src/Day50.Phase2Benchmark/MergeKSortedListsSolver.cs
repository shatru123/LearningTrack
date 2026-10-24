namespace Day50.Phase2Benchmark;

using System;
using System.Collections.Generic;

public class ListNode
{
    public int val;
    public ListNode? next;

    public ListNode(int val = 0, ListNode? next = null)
    {
        this.val = val;
        this.next = next;
    }
}

public class MergeKSortedListsSolver
{
    /// <summary>
    /// LeetCode #23: Merge k Sorted Lists.
    /// Optimal Min-Heap PriorityQueue: O(N log K) time, O(K) space.
    /// </summary>
    public static ListNode? MergeKListsMinHeap(ListNode?[] lists)
    {
        if (lists == null || lists.Length == 0) return null;

        var pq = new PriorityQueue<ListNode, int>();

        foreach (var head in lists)
        {
            if (head != null)
            {
                pq.Enqueue(head, head.val);
            }
        }

        var dummy = new ListNode(-1);
        var curr = dummy;

        while (pq.Count > 0)
        {
            var smallest = pq.Dequeue();
            curr.next = smallest;
            curr = curr.next;

            if (smallest.next != null)
            {
                pq.Enqueue(smallest.next, smallest.next.val);
            }
        }

        return dummy.next;
    }

    /// <summary>
    /// Divide-and-Conquer 2-way pairwise merge: O(N log K) time, O(1) auxiliary space.
    /// </summary>
    public static ListNode? MergeKListsDivideAndConquer(ListNode?[] lists)
    {
        if (lists == null || lists.Length == 0) return null;

        int interval = 1;
        while (interval < lists.Length)
        {
            for (int i = 0; i + interval < lists.Length; i += interval * 2)
            {
                lists[i] = MergeTwoLists(lists[i], lists[i + interval]);
            }
            interval *= 2;
        }

        return lists[0];
    }

    private static ListNode? MergeTwoLists(ListNode? l1, ListNode? l2)
    {
        var dummy = new ListNode(-1);
        var curr = dummy;

        while (l1 != null && l2 != null)
        {
            if (l1.val <= l2.val)
            {
                curr.next = l1;
                l1 = l1.next;
            }
            else
            {
                curr.next = l2;
                l2 = l2.next;
            }
            curr = curr.next;
        }

        curr.next = l1 ?? l2;
        return dummy.next;
    }

    // Helpers for test validation
    public static ListNode? FromArray(int[] values)
    {
        if (values == null || values.Length == 0) return null;
        var dummy = new ListNode(-1);
        var curr = dummy;
        foreach (var v in values)
        {
            curr.next = new ListNode(v);
            curr = curr.next;
        }
        return dummy.next;
    }

    public static int[] ToArray(ListNode? head)
    {
        var list = new List<int>();
        var curr = head;
        while (curr != null)
        {
            list.Add(curr.val);
            curr = curr.next;
        }
        return list.ToArray();
    }
}
