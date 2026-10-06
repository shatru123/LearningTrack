using System;
using System.Collections.Generic;

namespace Day15.ChannelsConcurrency;

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

public static class LinkedListSolvers
{
    /// <summary>
    /// LeetCode #206: Reverse Linked List (Iterative).
    /// Time: O(N), Space: O(1).
    /// </summary>
    public static ListNode? ReverseList(ListNode? head)
    {
        ListNode? prev = null;
        var curr = head;

        while (curr != null)
        {
            var nextTemp = curr.next;
            curr.next = prev;
            prev = curr;
            curr = nextTemp;
        }

        return prev;
    }

    /// <summary>
    /// LeetCode #206: Reverse Linked List (Recursive).
    /// Time: O(N), Space: O(N) call stack.
    /// </summary>
    public static ListNode? ReverseListRecursive(ListNode? head)
    {
        if (head == null || head.next == null)
            return head;

        var newHead = ReverseListRecursive(head.next);
        head.next.next = head;
        head.next = null;

        return newHead;
    }

    /// <summary>
    /// LeetCode #21: Merge Two Sorted Lists (Iterative with Sentinel Dummy Head).
    /// Time: O(N + M), Space: O(1).
    /// </summary>
    public static ListNode? MergeTwoLists(ListNode? list1, ListNode? list2)
    {
        var dummy = new ListNode(-1);
        var tail = dummy;

        while (list1 != null && list2 != null)
        {
            if (list1.val <= list2.val)
            {
                tail.next = list1;
                list1 = list1.next;
            }
            else
            {
                tail.next = list2;
                list2 = list2.next;
            }
            tail = tail.next;
        }

        tail.next = list1 ?? list2;
        return dummy.next;
    }

    /// <summary>
    /// LeetCode #21: Merge Two Sorted Lists (Recursive).
    /// Time: O(N + M), Space: O(N + M) call stack.
    /// </summary>
    public static ListNode? MergeTwoListsRecursive(ListNode? list1, ListNode? list2)
    {
        if (list1 == null) return list2;
        if (list2 == null) return list1;

        if (list1.val <= list2.val)
        {
            list1.next = MergeTwoListsRecursive(list1.next, list2);
            return list1;
        }
        else
        {
            list2.next = MergeTwoListsRecursive(list1, list2.next);
            return list2;
        }
    }
}
