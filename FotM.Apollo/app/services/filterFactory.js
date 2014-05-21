app.factory('filterFactory', function (media) {

    function FotMFilter(className, specId) {
        if (className == "null")
            className = null;

        this.className = className;

        if (specId) {
            this.specId = parseInt(specId);
        } else {
            this.specId = null;
        }
    }

    FotMFilter.fromString = function (str) {
        var myRegex = /([\w\s]+)(\((\d*)\))?/i;
        var match = myRegex.exec(str);

        var className = match[1];
        var specId = match[3];

        return new FotMFilter(className, specId);
    };

    // turns {className: Warrior, specId: 239} into Warrior(239)
    FotMFilter.prototype.toString = function () {
        if (this.className == null) return null;

        var str = this.className;
        if (this.specId == null) return str;

        return str + "(" + this.specId + ")";
    };


    return {

        create: function(className, specId) {
            return new FotMFilter(className, specId);
        },

        createFromString: function(str) {
            return FotMFilter.fromString(str);
        }

    };
});