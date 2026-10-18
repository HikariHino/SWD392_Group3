# Conceptual ERD — AIVES Complete System

This conceptual model shows the complete AIVES assignment using entity names and
business relationships only. Attributes are intentionally omitted.

```mermaid
erDiagram
    USER ||--o{ EXAM_SESSION : "creates or participates in"
    USER ||--o{ QUESTION : "manages"
    USER ||--o{ ASSESSMENT : "finalizes"
    COURSE ||--o{ QUESTION : "contains"
    COURSE ||--o{ EXAM_SESSION : "is examined in"
    QUESTION ||--|{ RUBRIC : "is evaluated by"
    EXAM_SESSION ||--o{ TRANSCRIPT : "records"
    USER ||--o{ TRANSCRIPT : "submits"
    TRANSCRIPT ||--o{ ASSESSMENT : "has"
    QUESTION ||--o{ ASSESSMENT : "is answered in"
```

`User`, `Course`, `Question`, and `Rubric` are implemented in the current
backend. `ExamSession`, `Transcript`, and `Assessment` belong to the planned
Flow 3/4 model and are included here because this is the conceptual ERD for the
complete assignment.
