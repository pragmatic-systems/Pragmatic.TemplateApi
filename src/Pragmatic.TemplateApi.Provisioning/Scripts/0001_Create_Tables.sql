-- Load Extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Create Tables

CREATE TABLE todo_list ( 
  item_id uuid NOT NULL DEFAULT gen_random_uuid (),
  title VARCHAR(128) NOT NULL,
  description VARCHAR(512) NOT NULL,
  due_date TIMESTAMPTZ NOT NULL,
  open boolean default true,
  closed_date TIMESTAMPTZ default NULL,
  PRIMARY KEY(item_id)
  );