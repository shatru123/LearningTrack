using System;
using System.Collections.Generic;

namespace Day16.SynchronizationLocks;

public class ListNode
{
    public int val;
    public ListNode? next;

    public ListNode(int val = 0, ListNode? next = null)
    {
        this.val = val;
        this.next = next;
    }

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

    public int[] ToArray()
    {
        var list = new List<int>();
        var curr = this;
        while (curr != null)
        {
            list.Add(curr.val);
            curr = curr.next;
        }
        return list.ToArray();
    }
}

public static class AdvancedLinkedListSolvers
{
    /// <summary>
    /// LeetCode #143: Reorder List
    /// L0 → L1 → … → Ln - 1 → Ln becomes L0 → Ln → L1 → Ln - 1 → L2 → Ln - 2 → …
    /// In-place. Time: O(N), Space: O(1).
    /// </summary>
    public static void ReorderList(ListNode? head)
    {
        if (head == null || head.next == null || head.next.next == null) return;

        // 1. Find the middle node using fast and slow pointers
        var slow = head;
        var fast = head;
        while (fast.next != null && fast.next.next != null)
        {
            slow = slow.next!;
            fast = fast.next.next;
        }

        // 2. Reverse the second half of the list
        var secondHalf = slow.next;
        slow.next = null; // Split into two halves

        ListNode? prev = null;
        var curr = secondHalf;
        while (curr != null)
        {
            var nextTemp = curr.next;
            curr.next = prev;
            prev = curr;
            curr = nextTemp;
        }
        var second = prev;

        // 3. Interleave the two halves
        var first = head;
        while (second != null)
        {
            var tmp1 = first!.next;
            var tmp2 = second.next;

            first.next = second;
            second.next = tmp1;

            first = tmp1;
            second = tmp2;
        }
    }

    /// <summary>
    /// LeetCode #19: Remove Nth Node From End of List
    /// One-pass with sentinel dummy node and fast/slow gap pointers.
    /// Time: O(N), Space: O(1).
    /// </summary>
    public static ListNode? RemoveNthFromEnd(ListNode? head, int n)
    {
        var dummy = new ListNode(0, head);
        var fast = dummy;
        var slow = dummy;

        // Advance fast by n + 1 steps
        for (int i = 0; i <= n; i++)
        {
            if (fast == null) return head; // n is larger than length
            fast = fast.next!;
        }

        // Move fast to the end, maintaining the gap
        while (fast != null)
        {
            fast = fast.next!;
            slow = slow.next!;
        }

        // Remove the target node
        slow.next = slow.next?.next;

        return dummy.next;
    }
}
