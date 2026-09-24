/** Moves the item at `from` to position `to`, returning the new order as old indices (a permutation). */
export function moveIndex(count: number, from: number, to: number): number[] {
	const order = Array.from({ length: count }, (_, i) => i);
	if (from === to || from < 0 || to < 0 || from >= count || to >= count) return order;
	const [moved] = order.splice(from, 1);
	order.splice(to, 0, moved);
	return order;
}

export function isIdentity(order: number[]): boolean {
	return order.every((v, i) => v === i);
}

/** Which grid slot a point is over; `rects` are the thumbs' bounding boxes in DOM order. */
export function slotAt(rects: DOMRectReadOnly[], x: number, y: number): number | null {
	for (let i = 0; i < rects.length; i++) {
		const r = rects[i];
		if (x >= r.left && x <= r.right && y >= r.top && y <= r.bottom) return i;
	}
	return null;
}
