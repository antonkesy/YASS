import { describe, expect, it } from 'vitest';
import { get } from 'svelte/store';
import { cameFrom } from '$lib/navigation';

describe('cameFrom', () => {
	it('is empty until a session says it is leaving', () => {
		expect(cameFrom.take()).toBeNull();
	});

	it('hands the id over once and then forgets it', () => {
		cameFrom.leaving('20260928-120000-abc123');

		expect(cameFrom.take()).toBe('20260928-120000-abc123');
		// The transition plays once; a later reload of home must not replay it.
		expect(cameFrom.take()).toBeNull();
		expect(get(cameFrom)).toBeNull();
	});
});
