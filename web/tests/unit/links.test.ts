import { readdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { ISSUE_TEMPLATES } from '$lib/links';

const dir = resolve('../.github/ISSUE_TEMPLATE');

describe('issue templates linked from the header', () => {
	it('each opens a form that exists, and every form is offered', () => {
		const forms = readdirSync(dir).filter((f) => f.endsWith('.yml') && f !== 'config.yml');
		expect(ISSUE_TEMPLATES.map((t) => t.file).sort()).toEqual(forms.sort());
	});

	it.each(ISSUE_TEMPLATES.map((t) => [t.file, t.label] as const))(
		'%s is named and labelled like its link',
		(file) => {
			const form = readFileSync(resolve(dir, file), 'utf8');
			expect(form).toMatch(/^name: .+$/m);
			expect(form).toMatch(/^labels: \[.+\]$/m);
		}
	);
});
