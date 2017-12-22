using System.Collections.Generic;

/**
 * This is a priority queue where the priority is only evaluated when an item should be popped.
 * Intended for usage where the priority changes often and reordering every time would be too time consuming.
 * */
public class LazyPriorityQueue<TValue> : List<TValue> {

    private IComparer<TValue> comparer;

    public LazyPriorityQueue(IComparer<TValue> _comparer) {
        this.comparer = _comparer;
    }

    public TValue Pop() {
        TValue topPrioValue = this[0];
        foreach (TValue val in this) {
            if (this.comparer.Compare(val, topPrioValue) < 0)
                topPrioValue = val;
        }
        return topPrioValue;
    }
}
