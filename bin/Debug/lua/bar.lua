require "cs2lua__utility";
require "cs2lua__namespaces";
require "cs2lua__externenums";
require "foo_CS_System_Int32_CS_System_Int32";

bar = {
	__new_object = function(...)
		return newobject(bar, nil, {}, ...);
	end,
	__define_class = function()
		local static = bar;

		local static_methods = {
			cctor = function()
				bar.__cctor();
			end,
			__cctor = function()
				if bar.__cctor_called then
					return;
				else
					bar.__cctor_called = true;
				end
				bar.s_DateTime = newexternobject(CS.System.DateTime, "CS.System.DateTime", nil, {});
			end,
		};

		local static_fields_build = function()
			local static_fields = {
				s_DateTime = __cs2lua_nil_field_value,
				__cctor_called = false,
			};
			return static_fields;
		end;
		local static_props = nil;
		local static_events = nil;

		local instance_methods = {
			test = function(this)
				local a; a = newobject(foo_CS_System_Int32_CS_System_Int32, "ctor", {});
				a:parse("123", "456");
				local b; b = this.m_DateTime:ToString();
				local c; c = bar.s_DateTime:ToString();
				local dt;
				local dt2;
			end,
			ctor = function(this)
				this:__ctor();
			end,
			__ctor = function(this)
				if this.__ctor_called then
					return;
				else
					this.__ctor_called = true;
				end
				this.m_DateTime = newexternobject(CS.System.DateTime, "CS.System.DateTime", nil, {});
			end,
		};

		local instance_fields_build = function()
			local instance_fields = {
				m_DateTime = __cs2lua_nil_field_value,
				__ctor_called = false,
			};
			return instance_fields;
		end;
		local instance_props = nil;
		local instance_events = nil;
		local interfaces = nil;
		local interface_map = nil;

		return defineclass(nil, "bar", static, static_methods, static_fields_build, static_props, static_events, instance_methods, instance_fields_build, instance_props, instance_events, interfaces, interface_map, false);
	end,
};

bar.__define_class();
