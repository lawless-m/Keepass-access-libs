//! Parsing and validation of `/`-separated entry paths.
//!
//! See `SPEC.md` § Path Lookup for the canonical rules:
//!
//! - the root group is implied and must not be included;
//! - the separator is `/`;
//! - matching is case-insensitive;
//! - leading/trailing whitespace on each segment is ignored;
//! - an empty path, or a path with only one segment, is an error.

use crate::error::KdbxError;

/// A validated, normalised lookup path.
///
/// Segments are trimmed and lowercased so that comparison against database
/// titles is a plain case-insensitive equality check.
pub(crate) struct ParsedPath {
    /// The group segments to walk from the (implied) root, in order.
    pub groups: Vec<String>,
    /// The entry title to match within the final group.
    pub title: String,
}

/// Parse and validate `path`, returning the normalised group/title split.
///
/// Returns [`KdbxError::InvalidPath`] for empty paths, single-segment paths,
/// paths containing an empty segment (e.g. a trailing `/`), or paths that
/// begin with the root group.
pub(crate) fn parse(path: &str) -> Result<ParsedPath, KdbxError> {
    let invalid = |reason: &'static str| KdbxError::InvalidPath {
        path: path.to_string(),
        reason,
    };

    if path.trim().is_empty() {
        return Err(invalid("path is empty"));
    }

    let segments: Vec<&str> = path.split('/').map(str::trim).collect();

    if segments.iter().any(|s| s.is_empty()) {
        return Err(invalid("path contains an empty segment"));
    }

    if segments.len() < 2 {
        return Err(invalid("path has a single segment — a group is required"));
    }

    if segments[0].eq_ignore_ascii_case("root") {
        return Err(invalid("the root group must not be included in the path"));
    }

    let (title, groups) = segments.split_last().expect("len >= 2 checked above");

    Ok(ParsedPath {
        groups: groups.iter().map(|s| s.to_lowercase()).collect(),
        title: title.to_lowercase(),
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    fn parse_ok(path: &str) -> ParsedPath {
        parse(path).expect("expected valid path")
    }

    #[test]
    fn splits_group_and_title() {
        let p = parse_ok("ndb/postgres-prod");
        assert_eq!(p.groups, vec!["ndb"]);
        assert_eq!(p.title, "postgres-prod");
    }

    #[test]
    fn supports_nested_groups() {
        let p = parse_ok("a/b/c/title");
        assert_eq!(p.groups, vec!["a", "b", "c"]);
        assert_eq!(p.title, "title");
    }

    #[test]
    fn normalises_case_and_whitespace() {
        let p = parse_ok(" NDB / Postgres-Prod ");
        assert_eq!(p.groups, vec!["ndb"]);
        assert_eq!(p.title, "postgres-prod");
    }

    #[test]
    fn rejects_empty() {
        assert!(matches!(parse(""), Err(KdbxError::InvalidPath { .. })));
        assert!(matches!(parse("   "), Err(KdbxError::InvalidPath { .. })));
    }

    #[test]
    fn rejects_single_segment() {
        assert!(matches!(
            parse("postgres-prod"),
            Err(KdbxError::InvalidPath { .. })
        ));
    }

    #[test]
    fn rejects_root_prefix() {
        assert!(matches!(
            parse("root/ndb/postgres-prod"),
            Err(KdbxError::InvalidPath { .. })
        ));
        assert!(matches!(
            parse("ROOT/ndb/x"),
            Err(KdbxError::InvalidPath { .. })
        ));
    }

    #[test]
    fn rejects_empty_segment() {
        assert!(matches!(
            parse("ndb//x"),
            Err(KdbxError::InvalidPath { .. })
        ));
        assert!(matches!(
            parse("ndb/x/"),
            Err(KdbxError::InvalidPath { .. })
        ));
    }
}
