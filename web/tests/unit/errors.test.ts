import { describe, expect, it } from 'vitest';
import { hubError } from '$lib/hub/errors';

describe('hubError', () => {
	it('unwraps a HubException, which is written for a person to read', () => {
		expect(
			hubError(
				new Error(
					"An unexpected error occurred invoking 'Export' on the server. HubException: " +
						'Export folder "/tmp/nope" is outside the allowed roots (/data/consume). ' +
						'Add it to output.allowed_roots to permit it.'
				)
			)
		).toBe(
			'Export folder "/tmp/nope" is outside the allowed roots (/data/consume). ' +
				'Add it to output.allowed_roots to permit it.'
		);
	});

	it('keeps a multi-line HubException intact', () => {
		expect(
			hubError(
				new Error(
					"An unexpected error occurred invoking 'Export' on the server. HubException: first\nsecond"
				)
			)
		).toBe('first\nsecond');
	});

	it('hides the internals of anything that is not a HubException', () => {
		expect(
			hubError(
				new Error(
					"An unexpected error occurred invoking 'Export' on the server. " +
						'NullReferenceException: Object reference not set to an instance of an object.'
				)
			)
		).toBe('Something went wrong on the server.');
	});

	it('passes through anything that is not a wrapped hub error', () => {
		expect(hubError(new Error('Failed to fetch'))).toBe('Failed to fetch');
		expect(hubError('plain string')).toBe('plain string');
	});
});
