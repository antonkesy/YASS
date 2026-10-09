/** The project on GitHub, and where issues are filed: both linked from the footer. */
export const REPO = 'https://github.com/antonkesy/YASS';

/**
 * The issue forms in .github/ISSUE_TEMPLATE, each opened straight on its own form. The
 * file names here have to match those files.
 */
export const ISSUE_TEMPLATES = [
	{ id: 'bug', label: 'Report a bug', hint: 'Something does not work', file: 'bug_report.yml' },
	{
		id: 'feature',
		label: 'Request a feature',
		hint: 'Something it could do',
		file: 'feature_request.yml'
	},
	{
		id: 'scanner',
		label: 'Scanner support',
		hint: 'A scanner is not found or misbehaves',
		file: 'scanner_support.yml'
	}
].map((t) => ({ ...t, href: `${REPO}/issues/new?template=${t.file}` }));
